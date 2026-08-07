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

    internal ScenarioClient(Uri baseAddress, int? maxConnectionsPerServer = null)
    {
        _freshConnectionPerRequest = maxConnectionsPerServer == null;
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
            Timeout = TimeSpan.FromSeconds(60),
            DefaultRequestVersion = HttpVersion.Version11,
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

    // A browser sends no charset here, so the default request encoding decides the reading.
    internal Task<ScenarioResponse> PostFormAsync(string path, string body) =>
        PostAsync(path, Encoding.UTF8.GetBytes(body), "application/x-www-form-urlencoded");

    internal Task<ScenarioResponse> PostBodyAsync(
        string path,
        byte[] body,
        bool chunked = false,
        bool expectContinue = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = chunked ? new DelayedChunkedContent(body) : new ByteArrayContent(body),
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
