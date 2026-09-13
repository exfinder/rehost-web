#nullable enable

using System.Text;
using System.Web.IisConfig;

namespace System.Web;

// Replaces IIS request filtering's verb, URL-length and query-length 404s.
internal static class RequestFiltering
{
    private const string VerbDenied = "404.6 Verb Denied";

    private const string UrlTooLong = "404.14 URL Too Long";

    private const string QueryStringTooLong = "404.15 Query String Too Long";

    internal static string? Judge(string verb, string? filePath, string? query)
    {
        var limits = IisServerConfiguration.Current.RequestLimits;
        if (!limits.AllowsVerb(verb))
        {
            return VerbDenied;
        }

        if (Bytes(filePath) > limits.MaxUrl)
        {
            return UrlTooLong;
        }

        return Bytes(query) > limits.MaxQueryString ? QueryStringTooLong : null;
    }

    private static int Bytes(string? value) =>
        value == null ? 0 : Encoding.UTF8.GetByteCount(value);
}
