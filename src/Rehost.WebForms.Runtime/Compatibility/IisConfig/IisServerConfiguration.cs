#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace System.Web.IisConfig;

// Shipped applicationHost baseline merged with the app root's <system.webServer> amendments,
// published atomically at activation. Unhonored sections are ignored, as Framework ignored the
// whole group. The app root is the only application file read: IIS ignores a subfolder <modules>
// section outright (MH24).
internal sealed class IisServerConfiguration
{
    private static readonly IisCollectionSchema MimeMapSchema =
        new("mimeMap", "fileExtension", "mimeType");

    private static readonly IisCollectionSchema HiddenSegmentSchema =
        new("add", "segment", null);

    private static volatile IisServerConfiguration _current = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new DefaultDocuments(enabled: true, Array.Empty<string>()),
        Array.Empty<IisRegistration>(),
        Array.Empty<IisRegistration>());

    private readonly Dictionary<string, string> _staticContent;
    private readonly Dictionary<string, string> _hiddenSegments;

    private IisServerConfiguration(
        Dictionary<string, string> staticContent,
        Dictionary<string, string> hiddenSegments,
        DefaultDocuments defaultDocuments,
        IReadOnlyList<IisRegistration> modules,
        IReadOnlyList<IisRegistration> handlers)
    {
        _staticContent = staticContent;
        _hiddenSegments = hiddenSegments;
        DefaultDocuments = defaultDocuments;
        Modules = modules;
        Handlers = handlers;
    }

    internal static IisServerConfiguration Current => _current;

    internal DefaultDocuments DefaultDocuments { get; }

    internal IReadOnlyList<IisRegistration> Modules { get; }

    internal IReadOnlyList<IisRegistration> Handlers { get; }

    internal bool ServesStaticContent(string? extension) =>
        !string.IsNullOrEmpty(extension) && _staticContent.ContainsKey(extension);

    internal string? StaticContentTypeOf(string? extension) =>
        !string.IsNullOrEmpty(extension) && _staticContent.TryGetValue(extension, out var mimeType)
            ? mimeType
            : null;

    internal bool IsHiddenSegment(string segment) => _hiddenSegments.ContainsKey(segment);

    internal static IisServerConfiguration Load(
        string baselineConfigPath,
        string applicationConfigPath)
    {
        var sections = new Sections();

        ApplyFile(baselineConfigPath, required: true, application: false, sections);
        sections.Modules.SealInheritance();
        sections.Handlers.SealInheritance();
        ApplyFile(applicationConfigPath, required: false, application: true, sections);

        return new IisServerConfiguration(
            sections.StaticContent,
            sections.HiddenSegments,
            sections.DefaultDocuments.Build(),
            sections.Modules.Build(),
            sections.Handlers.Build());
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
            ClassicSectionValidation.Validate(document, configPath);
        }

        var staticContentNode = document.SelectSingleNode(
            "/configuration/system.webServer/staticContent");
        if (staticContentNode != null)
        {
            IisCollectionReader.Apply(
                staticContentNode, MimeMapSchema, sections.StaticContent, configPath);
        }

        var hiddenSegmentsNode = document.SelectSingleNode(
            "/configuration/system.webServer/security/requestFiltering/hiddenSegments");
        if (hiddenSegmentsNode != null)
        {
            IisCollectionReader.Apply(
                hiddenSegmentsNode, HiddenSegmentSchema, sections.HiddenSegments, configPath);
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

        internal DefaultDocumentSection DefaultDocuments { get; } = new();

        internal IisRegistrationSection Modules { get; } = IisRegistrationSection.ForModules();

        internal IisRegistrationSection Handlers { get; } = IisRegistrationSection.ForHandlers();
    }
}
