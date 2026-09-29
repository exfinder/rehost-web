using System.Net;

namespace Rehost.Web.AspNetCore.Tests;

// Two flushes with a pause force chunked framing the server observes as a genuinely delayed
// body, not one buffered send.
internal sealed class DelayedChunkedContent(byte[] payload) : HttpContent
{
    protected override async Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context)
    {
        var first = payload.Length / 2;
        await stream.WriteAsync(payload.AsMemory(0, first));
        await stream.FlushAsync();
        await Task.Delay(50);
        await stream.WriteAsync(payload.AsMemory(first));
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
