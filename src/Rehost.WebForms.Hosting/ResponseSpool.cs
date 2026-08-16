namespace Rehost.WebForms.Hosting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;

internal readonly record struct ResponseHeader(string Name, string Value);

// System.Web writes a response through callbacks that assume a synchronous transport, which
// Kestrel does not offer. Output is therefore collected here as an ordered list of segments —
// buffered byte runs, and file ranges held by reference so the commit can hand them to the
// server's sendfile path instead of copying them through the buffer — and delivered in commits:
// the head (status, headers) once, then every segment not yet delivered. A response that never
// flushes is committed once after EndOfRequest seals it; a response System.Web flushes
// mid-request is committed at each flush, so its bytes reach the client when Framework's did.
internal sealed class ResponseSpool : IDisposable
{
    internal const int DefaultMemoryThreshold = 32 * 1024;

    private readonly record struct FileRange(string Path, long Offset, long Length);

    private readonly Func<string> _temporaryDirectoryAccessor;
    private readonly int _memoryThreshold;
    private readonly List<object> _segments = new();
    private readonly List<ResponseHeader> _headers = new();
    private FileBufferingWriteStream? _currentRun;
    private int _delivered;

    internal ResponseSpool(
        Func<string> temporaryDirectoryAccessor,
        int memoryThreshold = DefaultMemoryThreshold)
    {
        ArgumentNullException.ThrowIfNull(temporaryDirectoryAccessor);
        _temporaryDirectoryAccessor = temporaryDirectoryAccessor;
        _memoryThreshold = memoryThreshold;
    }

    internal int StatusCode { get; private set; } = 200;

    internal string ReasonPhrase { get; private set; } = "OK";

    // Held apart from the header list so the commit can assign HttpResponse.ContentLength rather
    // than append a second Content-Length beside the one the server derives.
    internal long? ContentLength { get; private set; }

    internal IReadOnlyList<ResponseHeader> Headers => _headers;

    internal bool IsSealed { get; private set; }

    internal bool HeadCommitted { get; private set; }

    // A delivery that failed on the transport (the client vanished mid-stream) leaves segments
    // half-drained; a later commit must not re-enter that stream. Once set, every commit no-ops.
    internal bool DeliveryFaulted { get; private set; }

    internal void SetStatus(int statusCode, string? reasonPhrase)
    {
        RequireHeadOpen();
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase ?? "";
    }

    internal void AddHeader(string name, string value)
    {
        RequireHeadOpen();
        _headers.Add(new ResponseHeader(name, value));
    }

    internal void SetContentLength(long contentLength)
    {
        RequireHeadOpen();
        ContentLength = contentLength;
    }

    internal void Write(byte[] data, int length)
    {
        ArgumentNullException.ThrowIfNull(data);
        RequireUnsealed();

        if (_currentRun == null)
        {
            _currentRun = new FileBufferingWriteStream(
                _memoryThreshold,
                bufferLimit: null,
                tempFileDirectoryAccessor: _temporaryDirectoryAccessor);
            _segments.Add(_currentRun);
        }

        _currentRun.Write(data, 0, length);
    }

    internal void WriteFile(string filename, long offset, long length)
    {
        RequireUnsealed();
        RequireRange(filename, offset, length);

        _currentRun = null;
        _segments.Add(new FileRange(filename, offset, length));
    }

    internal void Seal()
    {
        IsSealed = true;
    }

    // A mid-request flush: the head goes out even when no bytes are pending, as IIS sent
    // Framework's headers on the first Flush.
    internal Task FlushAsync(HttpContext context, CancellationToken cancellationToken)
    {
        return CommitAsync(context, cancellationToken, startWithoutBody: true);
    }

    // Publishes the head on the first call and every segment collected since the previous call.
    // The head alone does not start the response unless a flush asked for it: a body-less commit
    // keeps the server's own Content-Length: 0 ending.
    internal async Task CommitAsync(
        HttpContext context, CancellationToken cancellationToken, bool startWithoutBody = false)
    {
        if (DeliveryFaulted)
        {
            return;
        }

        var response = context.Response;
        if (!HeadCommitted)
        {
            response.StatusCode = StatusCode;

            // Without this the server substitutes the standard reason for the status code, and a
            // handler's own description is lost.
            var responseFeature = context.Features.Get<IHttpResponseFeature>();
            if (responseFeature != null)
            {
                responseFeature.ReasonPhrase = ReasonPhrase;
            }

            foreach (var header in _headers)
            {
                response.Headers.Append(header.Name, header.Value);
            }

            if (ContentLength.HasValue)
            {
                response.ContentLength = ContentLength;
            }

            HeadCommitted = true;
        }

        // Re-validate every pending range before the first of them leaves: a file that shrank
        // since it was spooled fails while a status can still be produced, not mid-body under
        // sent headers.
        for (var i = _delivered; i < _segments.Count; i++)
        {
            if (_segments[i] is FileRange range)
            {
                RequireRange(range.Path, range.Offset, range.Length);
            }
        }

        var pending = _delivered < _segments.Count;
        try
        {
            while (_delivered < _segments.Count)
            {
                var segment = _segments[_delivered];
                if (segment is FileRange range)
                {
                    await response.SendFileAsync(
                        range.Path, range.Offset, range.Length, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var run = (FileBufferingWriteStream)segment;
                    await run.DrainBufferAsync(response.Body, cancellationToken).ConfigureAwait(false);
                    run.Dispose();
                }

                _delivered++;
            }

            // A run delivered mid-request is closed; later writes open a new one.
            _currentRun = null;
            if (pending)
            {
                await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (startWithoutBody && !response.HasStarted)
            {
                await response.StartAsync(cancellationToken).ConfigureAwait(false);
                await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            // The client is gone; the half-drained segment must never be re-entered by the
            // terminal commit. The adapter turns this into the HttpException Framework raised.
            DeliveryFaulted = true;
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var segment in _segments)
        {
            (segment as FileBufferingWriteStream)?.Dispose();
        }
    }

    private static void RequireRange(string filename, long offset, long length)
    {
        var file = new FileInfo(filename);
        if (!file.Exists)
        {
            throw new FileNotFoundException(
                "The response file '" + filename + "' does not exist.", filename);
        }

        if (file.Length < offset + length)
        {
            throw new IOException(
                "The file '" + filename + "' ended " + (offset + length - file.Length)
                + " bytes before the requested range; it changed after its length was read.");
        }
    }

    // A transport-layer failure — the client vanished — rather than an application or spool error.
    internal static bool IsTransportFailure(Exception exception) =>
        exception is IOException or OperationCanceledException or ObjectDisposedException;

    private void RequireUnsealed()
    {
        if (IsSealed)
        {
            throw new InvalidOperationException(
                "The response was already sealed by EndOfRequest and cannot be modified.");
        }
    }

    // System.Web refuses status and header changes after its own headers went out; a call here
    // past the head commit is the adapter's mistake, not the application's.
    private void RequireHeadOpen()
    {
        RequireUnsealed();
        if (HeadCommitted)
        {
            throw new InvalidOperationException(
                "The response head was already committed by a flush and cannot be modified.");
        }
    }
}
