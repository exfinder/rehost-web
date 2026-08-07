namespace Rehost.WebForms.Hosting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

internal readonly record struct ResponseHeader(string Name, string Value);

// System.Web writes a response through callbacks that assume a synchronous transport, which
// Kestrel does not offer. Output is therefore collected here and committed once, after
// EndOfRequest seals it. The body is an ordered list of segments: buffered byte runs, and file
// ranges held by reference so the commit can hand them to the server's sendfile path instead of
// copying them through the buffer.
internal sealed class ResponseSpool : IDisposable
{
    internal const int DefaultMemoryThreshold = 32 * 1024;

    private readonly record struct FileRange(string Path, long Offset, long Length);

    private readonly Func<string> _temporaryDirectoryAccessor;
    private readonly int _memoryThreshold;
    private readonly List<object> _segments = new();
    private readonly List<ResponseHeader> _headers = new();
    private FileBufferingWriteStream? _currentRun;

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

    internal void SetStatus(int statusCode, string? reasonPhrase)
    {
        RequireUnsealed();
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase ?? "";
    }

    internal void AddHeader(string name, string value)
    {
        RequireUnsealed();
        _headers.Add(new ResponseHeader(name, value));
    }

    internal void SetContentLength(long contentLength)
    {
        RequireUnsealed();
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

    internal async Task CommitBodyAsync(HttpResponse response, CancellationToken cancellationToken)
    {
        if (!IsSealed)
        {
            throw new InvalidOperationException(
                "The response cannot be committed before EndOfRequest seals it.");
        }

        // Re-validate every range before the first byte leaves: a file that shrank since it was
        // spooled fails while a status can still be produced, not mid-body under sent headers.
        foreach (var segment in _segments)
        {
            if (segment is FileRange range)
            {
                RequireRange(range.Path, range.Offset, range.Length);
            }
        }

        foreach (var segment in _segments)
        {
            if (segment is FileRange range)
            {
                await response.SendFileAsync(
                    range.Path, range.Offset, range.Length, cancellationToken);
            }
            else
            {
                await ((FileBufferingWriteStream)segment)
                    .DrainBufferAsync(response.Body, cancellationToken);
            }
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

    private void RequireUnsealed()
    {
        if (IsSealed)
        {
            throw new InvalidOperationException(
                "The response was already sealed by EndOfRequest and cannot be modified.");
        }
    }
}
