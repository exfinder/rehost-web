#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Xml;

namespace System.Web.IisConfig;

// Shipped applicationHost baseline merged with the app root's <system.webServer> amendments,
// published atomically at activation. Unhonored sections are ignored, as Framework ignored the
// whole group. Only <handlers> is read from folder web.configs below the root, where IIS resolves
// it per folder (MH27) and ignores everything else it finds there, <modules> included (MH24).
internal sealed class IisServerConfiguration
{
    private static readonly IisCollectionSchema MimeMapSchema =
        new("mimeMap", "fileExtension", "mimeType");

    private static readonly IisCollectionSchema HiddenSegmentSchema =
        new("add", "segment", null);

    private static readonly IisCollectionSchema FileExtensionSchema =
        new("add", "fileExtension", "allowed");

    private static readonly IisCollectionSchema CachingProfileSchema =
        new("add", "extension", null);

    private static volatile IisServerConfiguration _current = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
        allowUnlistedExtensions: true,
        new Dictionary<string, IisCachingProfile>(StringComparer.OrdinalIgnoreCase),
        Array.Empty<string>(),
        new DefaultDocuments(enabled: true, Array.Empty<string>()),
        Array.Empty<IisRegistration>(),
        runAllManagedModulesForAllRequests: false,
        Array.Empty<IisRegistration>(),
        Array.Empty<IisHandlerRoute>(),
        IisFolderHandlers.Empty);

    private readonly Dictionary<string, string> _staticContent;
    private readonly Dictionary<string, string> _hiddenSegments;
    private readonly Dictionary<string, bool> _fileExtensions;
    private readonly bool _allowUnlistedExtensions;
    private readonly Dictionary<string, IisCachingProfile> _cachingProfiles;
    private readonly IisFolderHandlers _folderHandlers;

    private IisServerConfiguration(
        Dictionary<string, string> staticContent,
        Dictionary<string, string> hiddenSegments,
        Dictionary<string, bool> fileExtensions,
        bool allowUnlistedExtensions,
        Dictionary<string, IisCachingProfile> cachingProfiles,
        IReadOnlyList<string> userModeCachedExtensions,
        DefaultDocuments defaultDocuments,
        IReadOnlyList<IisRegistration> modules,
        bool runAllManagedModulesForAllRequests,
        IReadOnlyList<IisRegistration> handlers,
        IReadOnlyList<IisHandlerRoute> handlerRoutes,
        IisFolderHandlers folderHandlers)
    {
        _staticContent = staticContent;
        _hiddenSegments = hiddenSegments;
        _fileExtensions = fileExtensions;
        _allowUnlistedExtensions = allowUnlistedExtensions;
        _cachingProfiles = cachingProfiles;
        UserModeCachedExtensions = userModeCachedExtensions;
        DefaultDocuments = defaultDocuments;
        Modules = modules;
        RunAllManagedModulesForAllRequests = runAllManagedModulesForAllRequests;
        Handlers = handlers;
        HandlerRoutes = handlerRoutes;
        _folderHandlers = folderHandlers;
    }

    internal static IisServerConfiguration Current => _current;

    internal DefaultDocuments DefaultDocuments { get; }

    internal IReadOnlyList<IisRegistration> Modules { get; }

    // The flag nullifies the managedHandler condition for the whole collection (MH17), so the
    // dynamic registry's implicit condition falls with the configured ones.
    internal bool RunAllManagedModulesForAllRequests { get; }

    internal IReadOnlyList<IisRegistration> Handlers { get; }

    internal IReadOnlyList<IisHandlerRoute> HandlerRoutes { get; }

    internal IReadOnlyList<IisHandlerRoute> HandlerRoutesFor(VirtualPath? path) =>
        _folderHandlers.RoutesFor(path);

    internal bool ServesStaticContent(string? extension) =>
        !string.IsNullOrEmpty(extension) && _staticContent.ContainsKey(extension);

    internal string? StaticCacheControl(string? extension) =>
        !string.IsNullOrEmpty(extension)
        && _cachingProfiles.TryGetValue(extension, out var profile)
            ? profile.CacheControl
            : null;

    internal IReadOnlyList<string> UserModeCachedExtensions { get; }

    internal void ReportUnsupported(string applicationConfigurationFilePath)
    {
        ReportIgnoredCachingProfiles(applicationConfigurationFilePath);
    }

    // Static or dynamic is the handler list's answer for a request, not a fixed extension list.
    private void ReportIgnoredCachingProfiles(string applicationConfigurationFilePath)
    {
        var dynamicExtensions = new List<string>();
        foreach (var extension in UserModeCachedExtensions)
        {
            var route = IntegratedHandlers.Resolve(
                HandlerRoutes,
                "GET",
                VirtualPath.Create($"/probe{extension}"),
                pathTranslated: null,
                out _);
            if (route is { IsManaged: true })
            {
                dynamicExtensions.Add(extension);
            }
        }

        if (dynamicExtensions.Count != 0)
        {
            Util.WebFormsRuntimeEventSource.Log.CachingProfilesIgnored(
                applicationConfigurationFilePath,
                string.Join(", ", dynamicExtensions));
        }
    }

    internal string? StaticContentTypeOf(string? extension) =>
        !string.IsNullOrEmpty(extension) && _staticContent.TryGetValue(extension, out var mimeType)
            ? mimeType
            : null;

    internal bool IsHiddenSegment(string segment) => _hiddenSegments.ContainsKey(segment);

    // allowUnlisted="false" turns the deny list into an allow list, and an extensionless path
    // is then judged too - it carries the empty extension rather than skipping the rule (MH29).
    internal bool IsForbiddenExtension(string? extension) =>
        _fileExtensions.TryGetValue(extension ?? string.Empty, out var allowed)
            ? !allowed
            : !_allowUnlistedExtensions;

    internal static IisServerConfiguration Load(
        string baselineConfigPath,
        string applicationConfigPath,
        string applicationVirtualPath = "/")
    {
        var sections = new Sections();

        ApplyFile(baselineConfigPath, required: true, application: false, sections);
        sections.Modules.SealInheritance();
        sections.Handlers.SealInheritance();
        ApplyFile(applicationConfigPath, required: false, application: true, sections);

        var handlers = sections.Handlers.Build();
        var handlerRoutes = IisHandlerRoute.Build(handlers);

        return new IisServerConfiguration(
            sections.StaticContent,
            sections.HiddenSegments,
            sections.FileExtensionsParsed,
            sections.AllowUnlistedExtensions,
            sections.CachingProfiles,
            sections.UserModeCachedExtensions(),
            sections.DefaultDocuments.Build(),
            sections.Modules.Build(),
            sections.Modules.RunAllManagedModules,
            handlers,
            handlerRoutes,
            IisFolderHandlers.Load(
                sections.Handlers,
                handlerRoutes,
                Path.GetDirectoryName(Path.GetFullPath(applicationConfigPath))!,
                applicationVirtualPath,
                sections.ClassicSectionsWaived,
                sections.HiddenSegments));
    }

    internal static void Publish(IisServerConfiguration configuration)
    {
        _current = configuration;
    }

    private static void ApplyFile(
        string configPath,
        bool required,
        bool application,
        Sections sections)
    {
        if (!File.Exists(configPath))
        {
            if (required)
            {
                throw new FileNotFoundException(
                    "The IIS baseline configuration is missing: '" + configPath + "'.",
                    configPath);
            }

            return;
        }

        var document = new XmlDocument();
        document.Load(configPath);

        if (application)
        {
            sections.ClassicSectionsWaived =
                ClassicSectionValidation.Validate(document, configPath);
        }

        var staticContentNode = document.SelectSingleNode(
            "/configuration/system.webServer/staticContent");
        if (staticContentNode != null)
        {
            IisCollectionReader.Apply(
                staticContentNode, MimeMapSchema, sections.StaticContent, configPath);
        }

        var fileExtensionsNode = document.SelectSingleNode(
            "/configuration/system.webServer/security/requestFiltering/fileExtensions");
        if (fileExtensionsNode != null)
        {
            IisCollectionReader.Apply(
                fileExtensionsNode, FileExtensionSchema, sections.FileExtensions, configPath);

            var allowUnlisted = IisCollectionReader.OptionalBoolean(
                fileExtensionsNode, "fileExtensions", "allowUnlisted", configPath);
            if (allowUnlisted != null)
            {
                sections.AllowUnlistedExtensions = allowUnlisted.Value;
            }

            // Every row is re-parsed after each file so a bad value is reported against the file
            // that is being read, which is the one the author has to edit.
            sections.ParseFileExtensions(configPath);
        }

        var hiddenSegmentsNode = document.SelectSingleNode(
            "/configuration/system.webServer/security/requestFiltering/hiddenSegments");
        if (hiddenSegmentsNode != null)
        {
            IisCollectionReader.Apply(
                hiddenSegmentsNode, HiddenSegmentSchema, sections.HiddenSegments, configPath);
        }

        var cachingProfilesNode = document.SelectSingleNode(
            "/configuration/system.webServer/caching/profiles");
        if (cachingProfilesNode != null)
        {
            IisCollectionReader.Apply(
                cachingProfilesNode,
                CachingProfileSchema,
                sections.CachingProfiles,
                configPath,
                IisCachingProfile.Read);
        }

        var defaultDocumentNode = document.SelectSingleNode(
            "/configuration/system.webServer/defaultDocument");
        if (defaultDocumentNode != null)
        {
            sections.DefaultDocuments.Apply(defaultDocumentNode, configPath);
        }

        var modulesNode = document.SelectSingleNode("/configuration/system.webServer/modules");
        if (modulesNode != null)
        {
            sections.Modules.Apply(modulesNode, configPath);
        }

        var handlersNode = document.SelectSingleNode("/configuration/system.webServer/handlers");
        if (handlersNode != null)
        {
            sections.Handlers.Apply(handlersNode, configPath);
        }
    }

    private sealed class Sections
    {
        internal Dictionary<string, string> StaticContent { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        internal Dictionary<string, string> HiddenSegments { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        internal Dictionary<string, string> FileExtensions { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        internal Dictionary<string, bool> FileExtensionsParsed { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        internal bool AllowUnlistedExtensions { get; set; } = true;

        internal Dictionary<string, IisCachingProfile> CachingProfiles { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        internal IReadOnlyList<string> UserModeCachedExtensions()
        {
            var extensions = new List<string>();
            foreach (var entry in CachingProfiles)
            {
                if (entry.Value.StoresUserModeCopy)
                {
                    extensions.Add(entry.Key);
                }
            }

            extensions.Sort(StringComparer.OrdinalIgnoreCase);
            return extensions.ToArray();
        }

        internal void ParseFileExtensions(string configPath)
        {
            FileExtensionsParsed.Clear();
            foreach (var entry in FileExtensions)
            {
                FileExtensionsParsed[entry.Key] = ParseAllowed(entry.Key, entry.Value, configPath);
            }
        }

        private static bool ParseAllowed(string extension, string value, string configPath)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            throw new ConfigurationErrorsException(
                $"""<add fileExtension="{extension}" allowed="{value}"> in '{configPath}' """
                + IisCollectionReader.BooleanRule);
        }

        internal bool ClassicSectionsWaived { get; set; }

        internal DefaultDocumentSection DefaultDocuments { get; } = new();

        internal IisRegistrationSection Modules { get; } = IisRegistrationSection.ForModules();

        internal IisRegistrationSection Handlers { get; } = IisRegistrationSection.ForHandlers();
    }
}
