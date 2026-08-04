using System.Net;

namespace Rehost.WebForms.Hosting.Tests;

// Deterministic by construction: no redirects, cookies, proxy, or decompression, exact HTTP/1.1.
// Assertions see what the server sent, not what a convenience layer made of it.
internal sealed class ScenarioClient : IDisposable
{
    private readonly HttpClient _client;

    internal ScenarioClient(Uri baseAddress, int? maxConnectionsPerServer = null)
    {
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

    internal Task<ScenarioResponse> PostAsync(string path, byte[] body, string contentType)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        return SendAsync(request);
    }

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
                Bytes = bytes,
            };
        }
    }

    public void Dispose() => _client.Dispose();
}
