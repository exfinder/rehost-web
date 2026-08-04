using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace CoreParity.Probes;

public sealed class MultipartFile
{
    public MultipartFile(string name, string fileName, string contentType, byte[] content)
    {
        Name = name;
        FileName = fileName;
        ContentType = contentType;
        Content = content;
    }

    public string Name { get; }

    public string FileName { get; }

    public string ContentType { get; }

    public byte[] Content { get; }
}

// Builds the postback from what the previous response rendered rather than from a recorded
// literal, so each runtime posts back its own view state.
public static class PostbackFormClient
{
    private static readonly Regex InputTag = new Regex(
        "<input\\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Attribute = new Regex(
        "(?<name>[\\w:-]+)\\s*=\\s*\"(?<value>[^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FormTag = new Regex(
        "<form\\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string FormAction(string html)
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
                return HttpUtility.HtmlDecode(attribute.Groups["value"].Value);
            }
        }

        throw new InvalidOperationException("The rendered <form> carries no action attribute.");
    }

    // Submit buttons are excluded because a browser posts only the one that was clicked; the
    // caller names it through an override.
    public static Dictionary<string, string> ReadFields(string html)
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
                        name = HttpUtility.HtmlDecode(attribute.Groups["value"].Value);
                        break;
                    case "value":
                        value = HttpUtility.HtmlDecode(attribute.Groups["value"].Value);
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

    public static string BuildBody(string html, IDictionary<string, string> overrides)
    {
        var fields = ReadFields(html);

        foreach (var entry in overrides)
        {
            fields[entry.Key] = entry.Value;
        }

        return Encode(fields);
    }

    public const string MultipartBoundary = "RehostPostbackBoundary";

    public static byte[] EncodeMultipart(
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

    public static string Encode(IDictionary<string, string> fields)
    {
        var body = new StringBuilder();

        foreach (var entry in fields)
        {
            if (body.Length != 0)
            {
                body.Append('&');
            }

            body.Append(HttpUtility.UrlEncode(entry.Key));
            body.Append('=');
            body.Append(HttpUtility.UrlEncode(entry.Value));
        }

        return body.ToString();
    }
}
