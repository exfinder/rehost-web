namespace Rehost.WebForms.Hosting;

using Microsoft.AspNetCore.Http;

internal static class RequestUrls
{
    internal static string Absolute(HttpRequest request, string pathAndQuery) =>
        $"{request.Scheme}://{request.Host.Value}{pathAndQuery}";

    internal static string Resolve(HttpRequest request, string location) =>
        location.StartsWith('/') ? Absolute(request, location) : location;
}
