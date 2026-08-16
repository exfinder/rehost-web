using System.Net.Sockets;
using System.Text;

namespace Rehost.WebForms.Hosting.Tests;

// Client choreography a managed HttpClient cannot express: a fixed-length upload cut short by a
// linger-zero close mid-body. Returns the interim status so the caller asserts the handshake.
internal static class RawSocketProbe
{
    internal static async Task<int> AbortMidBodyAsync(Uri address, string path)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var client = new TcpClient();
        await client.ConnectAsync(address.Host, address.Port, timeout.Token);
        await using var stream = client.GetStream();

        var headers = $"""
            POST {path} HTTP/1.1
            Host: {address.Authority}
            Content-Length: 100
            Expect: 100-continue


            """.ReplaceLineEndings("\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), timeout.Token);
        await stream.FlushAsync(timeout.Token);

        var interim = await ReadStatusLineAsync(stream, timeout.Token);

        await stream.WriteAsync("partial"u8.ToArray(), timeout.Token);
        await stream.FlushAsync(timeout.Token);
        client.Client.LingerState = new LingerOption(true, 0);
        client.Close();

        return interim;
    }

    // Byte-true view of a response: HttpClient decodes header values as Latin-1, which would
    // disguise the wire encoding under assertion.
    internal static Task<byte[]> GetRawResponseAsync(Uri address, string path)
    {
        var request = $"""
            GET {path} HTTP/1.1
            Host: {address.Authority}
            Connection: close


            """.ReplaceLineEndings("\r\n");
        return SendRawAsync(address, Encoding.ASCII.GetBytes(request));
    }

    // The response as it arrives: one entry per socket read with the elapsed time since the
    // request went out, so a test can tell what reached the client before a server-side delay
    // elapsed. HttpClient would buffer that timing away.
    internal static async Task<List<(TimeSpan Elapsed, byte[] Bytes)>> ReadTimedAsync(
        Uri address, string path)
    {
        var request = $"""
            GET {path} HTTP/1.1
            Host: {address.Authority}
            Connection: close


            """.ReplaceLineEndings("\r\n");
        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync(address.Host, address.Port);
        await using var stream = client.GetStream();

        var clock = System.Diagnostics.Stopwatch.StartNew();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));
        await stream.FlushAsync();

        var arrivals = new List<(TimeSpan, byte[])>();
        var buffer = new byte[65536];
        while (true)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            arrivals.Add((clock.Elapsed, buffer[..read]));
        }

        return arrivals;
    }

    // A request whose response may leave the connection open (an upgrade, a kept-alive error):
    // whatever arrives until the server closes or goes quiet for two seconds.
    internal static async Task<byte[]> SendRawUntilQuietAsync(Uri address, byte[] request)
    {
        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync(address.Host, address.Port);
        await using var stream = client.GetStream();
        await stream.WriteAsync(request);
        await stream.FlushAsync();

        using var received = new MemoryStream();
        var buffer = new byte[65536];
        while (true)
        {
            using var quiet = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            int read;
            try
            {
                read = await stream.ReadAsync(buffer, quiet.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (read == 0)
            {
                break;
            }

            received.Write(buffer, 0, read);
        }

        return received.ToArray();
    }

    // The request exactly as given, for a Host, forwarded headers, or header bytes no managed
    // client would send.
    internal static async Task<byte[]> SendRawAsync(Uri address, byte[] request)
    {
        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync(address.Host, address.Port);
        await using var stream = client.GetStream();

        await stream.WriteAsync(request);
        await stream.FlushAsync();

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static async Task<int> ReadStatusLineAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var current = new byte[1];

        while (bytes.Count < 64 * 1024)
        {
            if (await stream.ReadAsync(current, cancellationToken) == 0)
            {
                throw new EndOfStreamException("The HTTP response ended before its headers.");
            }

            bytes.Add(current[0]);
            var count = bytes.Count;
            if (count >= 4
                && bytes[count - 4] == '\r'
                && bytes[count - 3] == '\n'
                && bytes[count - 2] == '\r'
                && bytes[count - 1] == '\n')
            {
                var head = Encoding.ASCII.GetString([.. bytes]);
                return int.Parse(head.Split(' ')[1]);
            }
        }

        throw new InvalidDataException("The HTTP response headers exceeded 64 KB.");
    }
}
