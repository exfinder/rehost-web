#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace System.Web.IisConfig;

// Shipped applicationHost baseline merged with the app root's <system.webServer> amendments,
// published atomically at activation. Unhonored sections are ignored, as Framework ignored the
// whole group.
internal sealed class IisServerConfiguration
{
    private static readonly IisCollectionSchema MimeMapSchema =
        new("mimeMap", "fileExtension", "mimeType");

    private static readonly IisCollectionSchema HiddenSegmentSchema =
        new("add", "segment", null);

    private static volatile IisServerConfiguration _current = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new DefaultDocuments(enabled: true, Array.Empty<string>()));

    private readonly Dictionary<string, string> _staticContent;
    private readonly Dictionary<string, string> _hiddenSegments;

    private IisServerConfiguration(
        Dictionary<string, string> staticContent,
        Dictionary<string, string> hiddenSegments,
        DefaultDocuments defaultDocuments)
    {
        _staticContent = staticContent;
        _hiddenSegments = hiddenSegments;
        DefaultDocuments = defaultDocuments;
    }

    internal static IisServerConfiguration Current => _current;

    internal DefaultDocuments DefaultDocuments { get; }

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
        var staticContent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hiddenSegments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var defaultDocuments = new DefaultDocumentSection();

        ApplyFile(baselineConfigPath, required: true, staticContent, hiddenSegments, defaultDocuments);
        ApplyFile(applicationConfigPath, required: false, staticContent, hiddenSegments, defaultDocuments);

        return new IisServerConfiguration(staticContent, hiddenSegments, defaultDocuments.Build());
    }

    internal static void Publish(IisServerConfiguration configuration)
    {
        _current = configuration;
    }

    private static void ApplyFile(
        string configPath,
        bool required,
        Dictionary<string, string> staticContent,
        Dictionary<string, string> hiddenSegments,
        DefaultDocumentSection defaultDocuments)
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

        var staticContentNode = document.SelectSingleNode(
            "/configuration/system.webServer/staticContent");
        if (staticContentNode != null)
        {
            IisCollectionReader.Apply(staticContentNode, MimeMapSchema, staticContent, configPath);
        }

        var hiddenSegmentsNode = document.SelectSingleNode(
            "/configuration/system.webServer/security/requestFiltering/hiddenSegments");
        if (hiddenSegmentsNode != null)
        {
            IisCollectionReader.Apply(
                hiddenSegmentsNode, HiddenSegmentSchema, hiddenSegments, configPath);
        }

        var defaultDocumentNode = document.SelectSingleNode(
            "/configuration/system.webServer/defaultDocument");
        if (defaultDocumentNode != null)
        {
            defaultDocuments.Apply(defaultDocumentNode, configPath);
        }
    }
}
