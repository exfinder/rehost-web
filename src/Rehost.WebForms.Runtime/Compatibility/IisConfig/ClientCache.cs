#nullable enable

using System.Configuration;
using System.Globalization;
using System.Text;
using System.Xml;

namespace System.Web.IisConfig;

internal enum ClientCacheMode
{
    NoControl,
    UseMaxAge,
    UseExpires,
    DisableCache,
}

internal sealed record ClientCache(
    ClientCacheMode Mode, TimeSpan MaxAge, string HttpExpires, string Custom)
{
    internal static ClientCache Default { get; } =
        new(ClientCacheMode.NoControl, TimeSpan.FromDays(1), string.Empty, string.Empty);

    internal string? CacheControl(string? profileWord)
    {
        var text = new StringBuilder();
        Append(text, profileWord);
        Append(text, Custom);
        Append(text, Mode switch
        {
            ClientCacheMode.UseMaxAge => string.Create(
                CultureInfo.InvariantCulture, $"max-age={(long)MaxAge.TotalSeconds}"),
            ClientCacheMode.DisableCache => "no-cache",
            _ => null,
        });

        return text.Length == 0 ? null : text.ToString();
    }

    internal string? Expires =>
        Mode == ClientCacheMode.UseExpires && HttpExpires.Length != 0 ? HttpExpires : null;

    private static void Append(StringBuilder text, string? part)
    {
        if (string.IsNullOrEmpty(part))
        {
            return;
        }

        if (text.Length != 0)
        {
            text.Append(',');
        }

        text.Append(part);
    }
}

internal sealed class ClientCacheSection
{
    private const string SpanRule =
        "is not a time span such as 1.00:00:00 or 00:00:30.";

    private const string EtagRule =
        "is not supported: the managed static handler writes the ETag with the response's cache"
        + " headers, where nothing in this port can take it back.";

    private const string TextRule =
        "carries a carriage return or line feed, which no header line can hold.";

    private ClientCacheMode _mode = ClientCacheMode.NoControl;
    private TimeSpan _maxAge = TimeSpan.FromDays(1);
    private string _httpExpires = string.Empty;
    private string _custom = string.Empty;

    internal ClientCache Build() => new(_mode, _maxAge, _httpExpires, _custom);

    internal void Apply(XmlDocument document, string configPath)
    {
        IisCollectionReader.RefuseInsideLocation(document, "clientCache", configPath);

        var section = document.SelectSingleNode(
            "/configuration/system.webServer/staticContent/clientCache");
        if (section == null)
        {
            return;
        }

        if (section.Attributes?["cacheControlMode"]?.Value is { } mode)
        {
            _mode = IisCollectionReader.EnumValue<ClientCacheMode>(
                $"""<clientCache cacheControlMode="{mode}">""", mode, configPath);
        }

        if (section.Attributes?["cacheControlMaxAge"]?.Value is { } span)
        {
            _maxAge = Span(span, configPath);
        }

        if (IisCollectionReader.OptionalBoolean(section, "clientCache", "setEtag", configPath)
            == false)
        {
            throw new ConfigurationErrorsException(
                $"""<clientCache setEtag="false"> in '{configPath}' {EtagRule}""");
        }

        _httpExpires = Text(section, "httpExpires", configPath) ?? _httpExpires;
        _custom = Text(section, "cacheControlCustom", configPath) ?? _custom;
    }

    private static TimeSpan Span(string value, string configPath)
    {
        if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var span))
        {
            throw new ConfigurationErrorsException(
                $"""<clientCache cacheControlMaxAge="{value}"> in '{configPath}' {SpanRule}""");
        }

        return span;
    }

    private static string? Text(XmlNode section, string attribute, string configPath)
    {
        var value = section.Attributes?[attribute]?.Value;
        if (value != null && value.AsSpan().ContainsAny('\r', '\n'))
        {
            throw new ConfigurationErrorsException(
                $"""<clientCache {attribute}="{value}"> in '{configPath}' {TextRule}""");
        }

        return value;
    }
}
