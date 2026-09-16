#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace System.Web.IisConfig;

// The leaf lists are the whole contract: an element on the way to a leaf is walked into, an element
// under a leaf is that reader's business, and anything else stops activation. A reader's own child
// elements therefore do not belong here.
internal static class UnhonoredSections
{
    internal const string CompressionReason =
        "this host does not compress responses. Add Response Compression middleware in Program.cs,"
        + " before UseRehostWebForms().";

    private static readonly string[] RootLeaves =
    [
        "handlers",
        "modules",
        "validation",
        "defaultDocument",
        "httpErrors",
        "rewrite",
        "staticContent",
        "httpProtocol/customHeaders",
        "caching/profiles",
        "security/requestFiltering/fileExtensions",
        "security/requestFiltering/hiddenSegments",
        "security/requestFiltering/requestLimits",
        "security/requestFiltering/verbs",
    ];

    private static readonly string[] FolderLeaves = ["handlers", "validation", "modules"];

    private static readonly string[] WarnedLeaves = ["urlCompression", "httpCompression"];

    private const string RootRule =
        "is not supported by this port. Remove it. Where a counterpart exists, it belongs in the"
        + " host's ASP.NET Core pipeline in Program.cs, before UseRehostWebForms().";

    private const string FolderRule =
        "is not supported by this port; a folder web.config honors <handlers>, <validation> and"
        + " <modules> only. Move it to the application root.";

    private const string LocationRule =
        "is not supported by this port; system.webServer is honored in the application root only."
        + " Move the block to the root, or to a web.config in that folder.";

    internal static IReadOnlyList<string> RefuseAtRoot(XmlDocument document, string configPath)
    {
        RefuseInsideLocation(document, configPath);

        var warned = new List<string>();
        Walk(Section(document), string.Empty, RootLeaves, RootRule, configPath, warned);
        return warned;
    }

    internal static void RefuseInFolder(XmlDocument document, string configPath)
    {
        RefuseInsideLocation(document, configPath);
        Walk(Section(document), string.Empty, FolderLeaves, FolderRule, configPath, null);
    }

    private static XmlNode? Section(XmlDocument document) =>
        document.SelectSingleNode("/configuration/system.webServer");

    private static void RefuseInsideLocation(XmlDocument document, string configPath)
    {
        var section = document.SelectSingleNode("//location/system.webServer");
        if (section != null)
        {
            var path = section.ParentNode?.Attributes?["path"]?.Value ?? string.Empty;
            throw new ConfigurationErrorsException(
                $"""<system.webServer> inside <location path="{path}"> in '{configPath}' {LocationRule}""");
        }
    }

    private static void Walk(
        XmlNode? parent,
        string prefix,
        string[] leaves,
        string rule,
        string configPath,
        List<string>? warned)
    {
        if (parent == null)
        {
            return;
        }

        foreach (XmlNode node in parent.ChildNodes)
        {
            if (node.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            var path = prefix.Length == 0 ? node.Name : $"{prefix}/{node.Name}";
            if (warned != null && Array.IndexOf(WarnedLeaves, path) >= 0)
            {
                warned.Add(path);
            }
            else if (Array.IndexOf(leaves, path) >= 0)
            {
                continue;
            }
            else if (LeadsToALeaf(leaves, path))
            {
                Walk(node, path, leaves, rule, configPath, warned);
            }
            else
            {
                throw new ConfigurationErrorsException($"<{path}> in '{configPath}' {rule}");
            }
        }
    }

    private static bool LeadsToALeaf(string[] leaves, string path)
    {
        foreach (var leaf in leaves)
        {
            if (leaf.Length > path.Length
                && leaf[path.Length] == '/'
                && leaf.StartsWith(path, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
