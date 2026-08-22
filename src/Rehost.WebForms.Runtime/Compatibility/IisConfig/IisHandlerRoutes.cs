#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Web.Util;

namespace System.Web.IisConfig;

internal enum IisResourceType
{
    Unspecified,
    File,
    Directory,
    Either,
}

internal enum IisNativeBridge
{
    None,
    StaticFile,
    ProtocolSupport,
}

// One handler row prepared for matching. Matching reuses the wildcard matchers the classic
// mapping already used, which carry the measured semantics: the path pattern is a case-
// insensitive segment-boundary suffix and the verb list is a case-sensitive comma alternation
// (MH28). The pattern is tested against the script path, so a URL continuing past it still
// matches (MH28).
internal sealed class IisHandlerRoute
{
    private const string ExtensionlessPattern = "*.";

    internal const string TransferRequestHandlerType = "System.Web.Handlers.TransferRequestHandler";

    private static readonly HashSet<string> StaticFileModules = new(StringComparer.Ordinal)
    {
        "StaticFileModule",
        "DefaultDocumentModule",
        "DirectoryListingModule",
    };

    private const string ProtocolSupportModuleName = "ProtocolSupportModule";

    private readonly WildcardUrl? _path;
    private readonly Wildcard _verb;

    internal IisHandlerRoute(IisRegistration registration)
    {
        Registration = registration;

        var path = registration.Path ?? string.Empty;
        if (!string.Equals(path, ExtensionlessPattern, StringComparison.Ordinal))
        {
            _path = new WildcardUrl(path, true /*caseInsensitive*/);
        }

        _verb = new Wildcard(
            (registration.Verb ?? "*").Replace(" ", string.Empty), false /*caseInsensitive*/);

        ResourceType = ParseResourceType(registration);
        Bridge = registration.Type == null ? ParseBridge(registration) : IisNativeBridge.None;
    }

    internal static IReadOnlyList<IisHandlerRoute> Build(IReadOnlyList<IisRegistration> handlers)
    {
        var routes = new IisHandlerRoute[handlers.Count];
        for (var index = 0; index < handlers.Count; index++)
        {
            routes[index] = new IisHandlerRoute(handlers[index]);
        }

        return routes;
    }

    internal IisRegistration Registration { get; }

    internal IisResourceType ResourceType { get; }

    internal IisNativeBridge Bridge { get; }

    internal bool IsManaged => Registration.Type != null;

    // TransferRequestHandler answers by spawning a child request that walks the list again
    // without the spawning row (MH9's follow-up), which for an unchanged URL is the rest of this
    // walk. Server.TransferRequest itself stays deferred, so the row is transparent to dispatch
    // rather than resolved; it still counts as a managed handler for preConditions.
    internal bool IsTransferRequest =>
        string.Equals(
            Registration.Type, TransferRequestHandlerType, StringComparison.Ordinal);

    internal bool Matches(string requestType, VirtualPath path) =>
        MatchesPath(path) && _verb.IsMatch(requestType);

    private bool MatchesPath(VirtualPath path) =>
        _path == null
            ? string.IsNullOrEmpty(path.Extension)
            : _path.IsSuffix(path.VirtualPathString);

    private static IisResourceType ParseResourceType(IisRegistration registration)
    {
        var value = registration.ResourceType;
        if (string.IsNullOrEmpty(value))
        {
            return IisResourceType.Unspecified;
        }

        if (Enum.TryParse<IisResourceType>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(IisResourceType), parsed))
        {
            return parsed;
        }

        throw new ConfigurationErrorsException(
            "<add name=\"" + registration.Name + "\"> in '" + registration.ConfigPath
            + "' carries resourceType=\"" + value + "\", which is not one of Unspecified, File,"
            + " Directory or Either.");
    }

    private static IisNativeBridge ParseBridge(IisRegistration registration)
    {
        var names = new List<string>();
        foreach (var token in (registration.Modules ?? string.Empty).Split(','))
        {
            var trimmed = token.Trim();
            if (trimmed.Length != 0)
            {
                names.Add(trimmed);
            }
        }

        if (names.Count == 0)
        {
            throw new ConfigurationErrorsException(
                "<add name=\"" + registration.Name + "\"> in '" + registration.ConfigPath
                + "' carries neither type= nor modules=, so nothing can serve the requests it"
                + " matches.");
        }

        if (names.Count == 1 && names[0] == ProtocolSupportModuleName)
        {
            return IisNativeBridge.ProtocolSupport;
        }

        foreach (var name in names)
        {
            if (!StaticFileModules.Contains(name))
            {
                throw new ConfigurationErrorsException(
                    "<add name=\"" + registration.Name + "\"> in '" + registration.ConfigPath
                    + "' names the native IIS module \"" + name + "\", which this runtime does not"
                    + " reimplement. Only StaticFileModule, DefaultDocumentModule,"
                    + " DirectoryListingModule and ProtocolSupportModule are bridged; a managed"
                    + " row carries type= instead.");
            }
        }

        return IisNativeBridge.StaticFile;
    }
}
