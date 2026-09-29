using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Rehost.Web.AspNetCore.Tests;

// Builds the postback from what the previous response rendered rather than from a recorded
// literal, so each runtime posts back its own view state. Encoding and decoding go through the
// BCL, never System.Web: activating an application is what the host child process is for, and
// HttpUtility reaches configuration that only an activated application has.
internal static class PostbackForm
{
    internal const string MultipartBoundary = "RehostPostbackBoundary";

    private static readonly Regex InputTag = new(
        "<input\\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Attribute = new(
        "(?<name>[\\w:-]+)\\s*=\\s*\"(?<value>[^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FormTag = new(
        "<form\\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static string Action(string html)
    {
        var form = FormTag.Match(html);
        if (!form.Success)
        {
            throw new InvalidOperationException("The response rendered no <form> element.");
        }

        foreach (Match attribute in Attribute.Matches(form.Value))
        {
            if (string.Equals(attribute.Groups["name"].Value, "action", StringComparison.OrdinalIgnoreCase))
            {
                return WebUtility.HtmlDecode(attribute.Groups["value"].Value);
            }
        }

        throw new InvalidOperationException("The rendered <form> carries no action attribute.");
    }

    // Submit buttons are excluded because a browser posts only the one that was clicked; the
    // caller names it through an override.
    internal static Dictionary<string, string> Fields(string html)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match tag in InputTag.Matches(html))
        {
            string? name = null;
            var value = "";
            var type = "text";

            foreach (Match attribute in Attribute.Matches(tag.Value))
            {
                switch (attribute.Groups["name"].Value.ToLowerInvariant())
                {
                    case "name":
                        name = WebUtility.HtmlDecode(attribute.Groups["value"].Value);
                        break;
                    case "value":
                        value = WebUtility.HtmlDecode(attribute.Groups["value"].Value);
                        break;
                    case "type":
                        type = attribute.Groups["value"].Value.ToLowerInvariant();
                        break;
                }
            }

            if (name != null && (type == "hidden" || type == "text"))
            {
                fields[name] = value;
            }
        }

        return fields;
    }

    internal static string Body(string html, IDictionary<string, string> overrides)
    {
        var fields = Fields(html);

        foreach (var entry in overrides)
        {
            fields[entry.Key] = entry.Value;
        }

        return Encode(fields);
    }

    internal static string Encode(IDictionary<string, string> fields)
    {
        var body = new StringBuilder();

        foreach (var entry in fields)
        {
            if (body.Length != 0)
            {
                body.Append('&');
            }

            body.Append(WebUtility.UrlEncode(entry.Key));
            body.Append('=');
            body.Append(WebUtility.UrlEncode(entry.Value));
        }

        return body.ToString();
    }

    internal static byte[] EncodeMultipart(
        IDictionary<string, string> fields,
        IList<MultipartFile> files)
    {
        var body = new MemoryStream();

        foreach (var entry in fields)
        {
            WriteAscii(body, "--" + MultipartBoundary + "\r\n");
            WriteAscii(body, "Content-Disposition: form-data; name=\"" + entry.Key + "\"\r\n\r\n");
            var value = Encoding.UTF8.GetBytes(entry.Value);
            body.Write(value, 0, value.Length);
            WriteAscii(body, "\r\n");
        }

        foreach (var file in files)
        {
            WriteAscii(body, "--" + MultipartBoundary + "\r\n");
            WriteAscii(
                body,
                "Content-Disposition: form-data; name=\"" + file.Name
                    + "\"; filename=\"" + file.FileName + "\"\r\n");
            WriteAscii(body, "Content-Type: " + file.ContentType + "\r\n\r\n");
            body.Write(file.Content, 0, file.Content.Length);
            WriteAscii(body, "\r\n");
        }

        WriteAscii(body, "--" + MultipartBoundary + "--\r\n");

        return body.ToArray();
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
