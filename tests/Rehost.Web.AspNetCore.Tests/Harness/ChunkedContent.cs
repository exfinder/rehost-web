using System.Net;

namespace Rehost.Web.AspNetCore.Tests;

// One write, so the client holds nothing back once the request is on the wire. A server that
// rejects mid-upload answers and closes; a client that writes after that close draws a reset,
// and Windows discards the already-received response with it.
internal sealed class ChunkedContent(byte[] payload) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        stream.WriteAsync(payload).AsTask();

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
