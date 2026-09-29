#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace System.Web.IisConfig;

// The merged <defaultDocument> section.
internal sealed class DefaultDocuments
{
    internal DefaultDocuments(bool enabled, IReadOnlyList<string> files)
    {
        Enabled = enabled;
        Files = files;
    }

    internal bool Enabled { get; }

    internal IReadOnlyList<string> Files { get; }
}

internal sealed class DefaultDocumentSection
{
    private readonly List<string> _files = new();
    private bool _enabled = true;

    internal DefaultDocuments Build() => new(_enabled, _files.ToArray());

    internal void Apply(XmlNode sectionNode, string configPath)
    {
        var enabledText = sectionNode.Attributes?["enabled"]?.Value;
        if (enabledText != null)
        {
            if (!bool.TryParse(enabledText, out var enabled))
            {
                throw new ConfigurationErrorsException(
                    "<defaultDocument enabled=\"" + enabledText + "\"> in '" + configPath
                    + "' is not a boolean.");
            }

            _enabled = enabled;
        }

        var filesNode = sectionNode.SelectSingleNode("files");
        if (filesNode == null)
        {
            return;
        }

        // The schema's files collection is mergeAppend="false": a file's adds go in front of
        // the inherited entries (reading D8).
        var added = new List<string>();
        foreach (XmlNode node in filesNode.ChildNodes)
        {
            if (node.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (node.Name == "clear")
            {
                _files.Clear();
                added.Clear();
            }
            else if (node.Name == "remove")
            {
                var value = RequireValue(node, configPath);
                _files.RemoveAll(f => Matches(f, value));
                added.RemoveAll(f => Matches(f, value));
            }
            else if (node.Name == "add")
            {
                var value = RequireValue(node, configPath);
                if (_files.Exists(f => Matches(f, value)) || added.Exists(f => Matches(f, value)))
                {
                    throw new ConfigurationErrorsException(
                        "<add value=\"" + value + "\"> in '" + configPath + "' duplicates an"
                        + " entry the <defaultDocument> files collection already contains; IIS"
                        + " refuses this with a 500.19. <remove> it first to reposition it.");
                }

                added.Add(value);
            }
            else
            {
                throw new ConfigurationErrorsException(
                    "'" + configPath + "' contains unsupported element <" + node.Name
                    + "> inside <defaultDocument><files>.");
            }
        }

        _files.InsertRange(0, added);
    }

    private static bool Matches(string entry, string value) =>
        string.Equals(entry, value, StringComparison.OrdinalIgnoreCase);

    private static string RequireValue(XmlNode node, string configPath)
    {
        var value = node.Attributes?["value"]?.Value;
        if (string.IsNullOrEmpty(value))
        {
            throw new ConfigurationErrorsException(
                "<" + node.Name + "> in '" + configPath + "' is missing its 'value' attribute.");
        }

        return value;
    }
}
