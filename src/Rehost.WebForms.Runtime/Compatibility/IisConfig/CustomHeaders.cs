#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace System.Web.IisConfig;

internal readonly record struct CustomHeader(string Name, string Value);

// requestFiltering's removeServerHeader is the only switch for the server's own Server header: a
// <remove name="Server" /> row is accepted and inert.
internal sealed record CustomHeaders(IReadOnlyList<CustomHeader> Rows, bool RemoveServerHeader)
{
    internal static CustomHeaders Empty { get; } =
        new(Array.Empty<CustomHeader>(), RemoveServerHeader: false);
}

internal sealed class CustomHeaderSection
{
    private const string TokenPunctuation = "!#$%&'*+-.^_`|~";

    private const string RootOnlyRule =
        "is honored only in the application root web.config.";

    private const string NameRule =
        $"is not an HTTP header name; only letters, digits and {TokenPunctuation} form one.";

    private const string ValueRule =
        "carries a carriage return or line feed in its value, which no header line can hold.";

    private static readonly IisCollectionSchema Schema = new("add", "name", "value");

    private readonly OrderedDictionary<string, string> _rows =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _removeServerHeader;

    internal CustomHeaders Build()
    {
        var rows = new CustomHeader[_rows.Count];
        var index = 0;
        foreach (var row in _rows)
        {
            rows[index++] = new CustomHeader(row.Key, row.Value);
        }

        return new CustomHeaders(rows, _removeServerHeader);
    }

    internal void Apply(XmlDocument document, string configPath)
    {
        if (document.SelectSingleNode("//location//customHeaders") != null)
        {
            throw new ConfigurationErrorsException(
                $"<customHeaders> inside <location> in '{configPath}' {RootOnlyRule}");
        }

        var section = document.SelectSingleNode(
            "/configuration/system.webServer/httpProtocol/customHeaders");
        if (section != null)
        {
            IisCollectionReader.Apply(section, Schema, _rows, configPath, ReadValue);
        }

        var requestFiltering = document.SelectSingleNode(
            "/configuration/system.webServer/security/requestFiltering");
        _removeServerHeader = IisCollectionReader.OptionalBoolean(
                requestFiltering, "requestFiltering", "removeServerHeader", configPath)
            ?? _removeServerHeader;
    }

    internal static void RefuseBelowTheRoot(XmlDocument document, string configPath)
    {
        if (document.SelectSingleNode("//customHeaders") != null)
        {
            throw new ConfigurationErrorsException(
                $"<customHeaders> in '{configPath}' {RootOnlyRule}");
        }
    }

    // IIS wrote a configured name and value onto the wire unvalidated; Kestrel faults the
    // response instead, so a row that could not be written is refused while the file is readable.
    private static string ReadValue(XmlNode node, string configPath)
    {
        var name = IisCollectionReader.RequireAttribute(node, "name", configPath);
        if (!IsToken(name))
        {
            throw new ConfigurationErrorsException(
                $"""<add name="{name}"> in '{configPath}' {NameRule}""");
        }

        var value = node.Attributes?["value"]?.Value ?? string.Empty;
        if (value.AsSpan().ContainsAny('\r', '\n'))
        {
            throw new ConfigurationErrorsException(
                $"""<add name="{name}"> in '{configPath}' {ValueRule}""");
        }

        return value;
    }

    private static bool IsToken(string name)
    {
        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character) && !TokenPunctuation.Contains(character))
            {
                return false;
            }
        }

        return true;
    }
}
