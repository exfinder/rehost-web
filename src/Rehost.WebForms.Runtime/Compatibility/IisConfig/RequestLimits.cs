#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Xml;

namespace System.Web.IisConfig;

internal sealed record RequestLimits(
    long MaxAllowedContentLength,
    long MaxUrl,
    long MaxQueryString,
    IReadOnlyDictionary<string, bool> Verbs,
    bool AllowUnlistedVerbs)
{
    internal static RequestLimits Unlimited { get; } = new(
        long.MaxValue,
        long.MaxValue,
        long.MaxValue,
        new Dictionary<string, bool>(StringComparer.Ordinal),
        AllowUnlistedVerbs: true);

    internal bool AllowsVerb(string verb) =>
        Verbs.TryGetValue(verb, out var allowed) ? allowed : AllowUnlistedVerbs;
}

internal sealed class RequestLimitsSection
{
    private const string NumberRule =
        "is not a byte count; IIS accepts only a non-negative whole number.";

    private static readonly IisCollectionSchema VerbSchema = new("add", "verb", "allowed");

    // Ordinal: IIS matched a row against the request verb exactly, so "get" is not "GET".
    private readonly Dictionary<string, bool> _verbs = new(StringComparer.Ordinal);

    private long _maxAllowedContentLength = long.MaxValue;
    private long _maxUrl = long.MaxValue;
    private long _maxQueryString = long.MaxValue;
    private bool _allowUnlistedVerbs = true;

    internal RequestLimits Build() => new(
        _maxAllowedContentLength,
        _maxUrl,
        _maxQueryString,
        new Dictionary<string, bool>(_verbs, StringComparer.Ordinal),
        _allowUnlistedVerbs);

    internal void Apply(XmlDocument document, string configPath)
    {
        var limits = document.SelectSingleNode(
            "/configuration/system.webServer/security/requestFiltering/requestLimits");
        if (limits != null)
        {
            _maxAllowedContentLength =
                Number(limits, "maxAllowedContentLength", configPath) ?? _maxAllowedContentLength;
            _maxUrl = Number(limits, "maxUrl", configPath) ?? _maxUrl;
            _maxQueryString = Number(limits, "maxQueryString", configPath) ?? _maxQueryString;
        }

        var verbs = document.SelectSingleNode(
            "/configuration/system.webServer/security/requestFiltering/verbs");
        if (verbs != null)
        {
            IisCollectionReader.Apply(verbs, VerbSchema, _verbs, configPath, ReadAllowed);
            _allowUnlistedVerbs = IisCollectionReader.OptionalBoolean(
                verbs, "verbs", "allowUnlisted", configPath) ?? _allowUnlistedVerbs;
        }
    }

    private static long? Number(XmlNode node, string attribute, string configPath)
    {
        var value = node.Attributes?[attribute]?.Value;
        if (value == null)
        {
            return null;
        }

        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            throw new ConfigurationErrorsException(
                $"""<requestLimits {attribute}="{value}"> in '{configPath}' {NumberRule}""");
        }

        return number;
    }

    private static bool ReadAllowed(XmlNode node, string configPath)
    {
        var verb = IisCollectionReader.RequireAttribute(node, "verb", configPath);
        var value = IisCollectionReader.RequireAttribute(node, "allowed", configPath);
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new ConfigurationErrorsException(
            $"""<add verb="{verb}" allowed="{value}"> in '{configPath}' """
            + IisCollectionReader.BooleanRule);
    }
}
