using System.Net;
using System.Text;

namespace Rehost.WebForms.Hosting.Tests;

// Deterministic by construction: no redirects, cookies, proxy, or decompression, exact HTTP/1.1,
// and one connection per request unless a test's claim is about connection reuse
// (maxConnectionsPerServer opts back into pooling). Reuse is hidden cross-request state: a
// pooled connection the server deliberately aborted can race its own teardown into the next
// request. Assertions see what the server sent, not what a convenience layer made of it.
internal sealed class ScenarioClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _freshConnectionPerRequest;
    private readonly bool _http2;

    // http2 speaks h2c with prior knowledge over the plain endpoint (exact version, no upgrade).
    internal ScenarioClient(Uri baseAddress, int? maxConnectionsPerServer = null, bool http2 = false)
    {
        _freshConnectionPerRequest = maxConnectionsPerServer == null && !http2;
        _http2 = http2;
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.None,
        };
        if (maxConnectionsPerServer is { } cap)
        {
            handler.MaxConnectionsPerServer = cap;
        }

        _client = new HttpClient(handler)
        {
            BaseAddress = baseAddress,

            // A wedged-server detector, not a performance assertion: matches LiveScenario's
            // startup timeout (the raw-socket probe holds the same ceiling). First-hit page
            // compilation runs inside the measured request and spends 10s+ of this budget on an
            // idle machine, so a tighter ceiling fails one unlucky request under concurrent load.
            Timeout = TimeSpan.FromSeconds(60),
            DefaultRequestVersion = http2 ? HttpVersion.Version20 : HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
    }

    internal Task<ScenarioResponse> GetAsync(string path) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, path));

    // The handler's cookie container stays off, so a request carries exactly the Cookie headers
    // named here and a response is read as the raw Set-Cookie lines the server wrote.
    internal Task<ScenarioResponse> GetWithCookiesAsync(string path, params string[] cookieHeaders)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        foreach (var header in cookieHeaders)
        {
            request.Headers.TryAddWithoutValidation("Cookie", header);
        }

        return SendAsync(request);
    }

    internal Task<ScenarioResponse> HeadAsync(string path) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Head, path));

    internal Task<ScenarioResponse> DeleteAsync(string path) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Delete, path));

    internal Task<ScenarioResponse> GetWithHeadersAsync(
        string path, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return SendAsync(request);
    }

    internal Task<ScenarioResponse> PostAsync(string path, byte[] body, string contentType)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        return SendAsync(request);
    }

    internal Task<ScenarioResponse> PostWithHeadersAsync(
        string path, byte[] body, string contentType, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return SendAsync(request);
    }

    // A browser sends no charset here, so the default request encoding decides the reading.
    internal Task<ScenarioResponse> PostFormAsync(string path, string body) =>
        PostAsync(path, Encoding.UTF8.GetBytes(body), "application/x-www-form-urlencoded");

    internal Task<ScenarioResponse> PostBodyAsync(
        string path,
        byte[] body,
        BodyFraming framing = BodyFraming.Fixed,
        bool expectContinue = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = framing switch
            {
                BodyFraming.Chunked => new ChunkedContent(body),
                BodyFraming.DelayedChunked => new DelayedChunkedContent(body),
                _ => new ByteArrayContent(body),
            },
        };
        if (expectContinue)
        {
            request.Headers.ExpectContinue = true;
        }

        return SendAsync(request);
    }

    private async Task<ScenarioResponse> SendAsync(HttpRequestMessage request)
    {
        if (_freshConnectionPerRequest)
        {
            request.Headers.ConnectionClose = true;
        }

        // HttpRequestMessage carries its own version; the client's default covers only the
        // convenience overloads.
        request.Version = _http2 ? HttpVersion.Version20 : HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

        using (request)
        using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead))
        {
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var headers = response.Headers
                .Concat(response.Content.Headers)
                .ToDictionary(
                    header => header.Key,
                    header => string.Join(", ", header.Value),
                    StringComparer.OrdinalIgnoreCase);

            return new ScenarioResponse
            {
                Version = response.Version,
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase ?? "",
                Headers = headers,
                SetCookies = response.Headers.TryGetValues("Set-Cookie", out var setCookies)
                    ? setCookies.ToArray()
                    : [],
                Bytes = bytes,
            };
        }
    }

    public void Dispose() => _client.Dispose();
}
