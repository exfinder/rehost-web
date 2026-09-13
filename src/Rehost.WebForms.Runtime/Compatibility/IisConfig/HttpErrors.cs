#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Web.Util;
using System.Xml;

namespace System.Web.IisConfig;

internal enum HttpErrorMode
{
    DetailedLocalOnly,
    Custom,
    Detailed,
}

internal enum ExistingResponse
{
    Auto,
    PassThrough,
    Replace,
}

internal enum HttpErrorRowMode
{
    BuiltIn,
    File,
    Redirect,
}

internal sealed record HttpErrorRow(
    int Status,
    int SubStatus,
    HttpErrorRowMode Mode,
    string? PhysicalPath,
    string? ContentType,
    string? Location);

internal sealed record HttpErrors(
    HttpErrorMode ErrorMode,
    ExistingResponse ExistingResponse,
    IReadOnlyList<HttpErrorRow> Rows)
{
    internal static HttpErrors Default { get; } = new(
        HttpErrorMode.DetailedLocalOnly,
        ExistingResponse.Auto,
        Array.Empty<HttpErrorRow>());

    internal HttpErrorRow? Find(int status, int subStatus)
    {
        HttpErrorRow? wildcard = null;
        foreach (var row in Rows)
        {
            if (row.Status != status)
            {
                continue;
            }

            if (row.SubStatus == subStatus)
            {
                return row;
            }

            if (row.SubStatus == Wildcard)
            {
                wildcard = row;
            }
        }

        return wildcard;
    }

    internal bool DetailedFor(bool isLocal) => ErrorMode switch
    {
        HttpErrorMode.Detailed => true,
        HttpErrorMode.DetailedLocalOnly => isLocal,
        _ => false,
    };

    internal const int Wildcard = -1;
}

internal sealed class HttpErrorsSection
{
    private const string RootOnlyRule =
        "is honored only in the application root web.config.";

    private const string LockedRule =
        "is locked below the server, where IIS refused it with a 500.19.";

    private const string ExecuteUrlRule =
        "is not supported: the error page would run as a second managed request.";

    private static readonly IisCollectionSchema Schema =
        new("error", ["statusCode", "subStatusCode"], null);

    private readonly OrderedDictionary<string, Draft> _rows = new(StringComparer.Ordinal);

    private HttpErrorMode _errorMode = HttpErrorMode.DetailedLocalOnly;
    private ExistingResponse _existingResponse = ExistingResponse.Auto;
    private ResponseMode _defaultResponseMode = ResponseMode.File;

    private enum ResponseMode
    {
        File,
        ExecuteURL,
        Redirect,
    }

    internal void Apply(XmlDocument document, string configPath)
    {
        if (document.SelectSingleNode("//location//httpErrors") != null)
        {
            throw new ConfigurationErrorsException(
                $"<httpErrors> inside <location> in '{configPath}' {RootOnlyRule}");
        }

        var section = document.SelectSingleNode("/configuration/system.webServer/httpErrors");
        if (section == null)
        {
            return;
        }

        RefuseLocked(section, "defaultPath", configPath);
        RefuseLocked(section, "allowAbsolutePathsWhenDelegated", configPath);

        _errorMode = Parse<HttpErrorMode>(section, "errorMode", configPath) ?? _errorMode;
        _existingResponse =
            Parse<ExistingResponse>(section, "existingResponse", configPath) ?? _existingResponse;
        _defaultResponseMode =
            Parse<ResponseMode>(section, "defaultResponseMode", configPath) ?? _defaultResponseMode;

        IisCollectionReader.Apply(section, Schema, _rows, configPath, ReadRow, Key);
    }

    internal static void RefuseBelowTheRoot(XmlDocument document, string configPath)
    {
        if (document.SelectSingleNode("//httpErrors") != null)
        {
            throw new ConfigurationErrorsException(
                $"<httpErrors> in '{configPath}' {RootOnlyRule}");
        }
    }

    internal HttpErrors Build(string applicationRoot, Func<string, string?> contentTypeOf)
    {
        var rows = new HttpErrorRow[_rows.Count];
        var index = 0;
        foreach (var entry in _rows)
        {
            rows[index++] = Resolve(entry.Value, applicationRoot, contentTypeOf);
        }

        return new HttpErrors(_errorMode, _existingResponse, rows);
    }

    private static HttpErrorRow Resolve(
        Draft draft, string applicationRoot, Func<string, string?> contentTypeOf)
    {
        if (draft.Mode != HttpErrorRowMode.File)
        {
            return new HttpErrorRow(
                draft.Status, draft.SubStatus, draft.Mode, null, null, draft.Path);
        }

        var combined = Path.GetFullPath(Path.Combine(
            applicationRoot, draft.Path!.Replace('/', Path.DirectorySeparatorChar)));
        var resolved = CanonicalCasePath.Resolve(combined, applicationRoot);
        if (!IsWithin(resolved, applicationRoot))
        {
            throw new ConfigurationErrorsException(
                $"""
                <error statusCode="{draft.Status}" path="{draft.Path}"> in '{draft.ConfigPath}' resolves to '{resolved}', which is outside the application root '{applicationRoot}'.
                """);
        }

        return new HttpErrorRow(
            draft.Status,
            draft.SubStatus,
            HttpErrorRowMode.File,
            resolved,
            contentTypeOf(Path.GetExtension(resolved)) ?? "application/octet-stream",
            null);
    }

    private static bool IsWithin(string path, string root)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(root);
        return path.Length > trimmed.Length
            && path.StartsWith(trimmed, StringComparison.Ordinal)
            && path[trimmed.Length] == Path.DirectorySeparatorChar;
    }

    private static string Key(XmlNode node, string configPath)
    {
        var (status, subStatus) = Identity(node, configPath);
        return string.Create(CultureInfo.InvariantCulture, $"{status},{subStatus}");
    }

    private static (int Status, int SubStatus) Identity(XmlNode node, string configPath)
    {
        IisCollectionReader.RequireAttribute(node, "statusCode", configPath);
        return (
            Number(node, "statusCode", 400, 999, configPath)!.Value,
            Number(node, "subStatusCode", HttpErrors.Wildcard, 999, configPath)
                ?? HttpErrors.Wildcard);
    }

    private Draft ReadRow(XmlNode node, string configPath)
    {
        var (status, subStatus) = Identity(node, configPath);

        if (node.Attributes?["prefixLanguageFilePath"] != null)
        {
            return new Draft(status, subStatus, HttpErrorRowMode.BuiltIn, null, configPath);
        }

        var explicitMode = Parse<ResponseMode>(node, "responseMode", configPath);
        var responseMode = explicitMode ?? _defaultResponseMode;
        if (responseMode == ResponseMode.ExecuteURL)
        {
            throw new ConfigurationErrorsException(explicitMode == null
                ? $"""
                  <error statusCode="{status}"> in '{configPath}' takes the section's defaultResponseMode="ExecuteURL", which {ExecuteUrlRule}
                  """
                : $"""
                  <error statusCode="{status}" responseMode="ExecuteURL"> in '{configPath}' {ExecuteUrlRule}
                  """);
        }

        var path = IisCollectionReader.RequireAttribute(node, "path", configPath);
        if (responseMode == ResponseMode.Redirect)
        {
            if (!path.StartsWith('/') && !Uri.IsWellFormedUriString(path, UriKind.Absolute))
            {
                throw new ConfigurationErrorsException(
                    $"""
                    <error statusCode="{status}" path="{path}"> in '{configPath}' is neither a site-absolute path nor an absolute URL, which a redirect target must be.
                    """);
            }

            return new Draft(status, subStatus, HttpErrorRowMode.Redirect, path, configPath);
        }

        if (Path.IsPathRooted(path))
        {
            throw new ConfigurationErrorsException(
                $"""
                <error statusCode="{status}" path="{path}"> in '{configPath}' is an absolute path; a file row names a path relative to the application root.
                """);
        }

        return new Draft(status, subStatus, HttpErrorRowMode.File, path, configPath);
    }

    private static void RefuseLocked(XmlNode section, string attribute, string configPath)
    {
        var value = section.Attributes?[attribute]?.Value;
        if (value != null)
        {
            throw new ConfigurationErrorsException(
                $"""<httpErrors {attribute}="{value}"> in '{configPath}' {LockedRule}""");
        }
    }

    private static int? Number(
        XmlNode node, string attribute, int minimum, int maximum, string configPath)
    {
        var value = node.Attributes?[attribute]?.Value;
        if (value == null)
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            || number < minimum
            || number > maximum)
        {
            throw new ConfigurationErrorsException(
                $"""
                <{node.Name} {attribute}="{value}"> in '{configPath}' is outside the range IIS accepts, {minimum} to {maximum}.
                """);
        }

        return number;
    }

    private static TValue? Parse<TValue>(XmlNode node, string attribute, string configPath)
        where TValue : struct, Enum
    {
        var value = node.Attributes?[attribute]?.Value;
        return value == null
            ? null
            : IisCollectionReader.EnumValue<TValue>(
                $"""<{node.Name} {attribute}="{value}">""", value, configPath);
    }

    private sealed record Draft(
        int Status, int SubStatus, HttpErrorRowMode Mode, string? Path, string ConfigPath);
}
