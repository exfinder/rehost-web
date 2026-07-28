namespace Rehost.WebForms.Hosting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;

internal readonly record struct ResponseHeader(string Name, string Value);

// System.Web writes a response through callbacks that assume a synchronous transport, which
// Kestrel does not offer. Output is therefore collected here and committed once, after
// EndOfRequest seals it.
internal sealed class ResponseSpool : IDisposable
{
    internal const int DefaultMemoryThreshold = 32 * 1024;

    private readonly FileBufferingWriteStream _body;
    private readonly List<ResponseHeader> _headers = new();

    internal ResponseSpool(
        Func<string> temporaryDirectoryAccessor,
        int memoryThreshold = DefaultMemoryThreshold)
    {
        ArgumentNullException.ThrowIfNull(temporaryDirectoryAccessor);

        _body = new FileBufferingWriteStream(
            memoryThreshold,
            bufferLimit: null,
            tempFileDirectoryAccessor: temporaryDirectoryAccessor);
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
        _body.Write(data, 0, length);
    }

    internal void Seal()
    {
        IsSealed = true;
    }

    internal Task DrainAsync(Stream destination, CancellationToken cancellationToken)
    {
        if (!IsSealed)
        {
            throw new InvalidOperationException(
                "The response cannot be committed before EndOfRequest seals it.");
        }

        return _body.DrainBufferAsync(destination, cancellationToken);
    }

    public void Dispose()
    {
        _body.Dispose();
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
