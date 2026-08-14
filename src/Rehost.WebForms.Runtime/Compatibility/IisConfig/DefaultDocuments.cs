#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace System.Web.IisConfig;

// The merged <defaultDocument> section. Unlike the other honored sections, a broken one defers
// its failure to consumption (readings D11/D13): IIS answers 500.19 only for the requests that
// read the section — directory requests — while direct requests keep serving.
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
    private ConfigurationErrorsException? _error;

    internal void Apply(XmlNode sectionNode, string configPath)
    {
        if (_error != null)
        {
            return;
        }

        try
        {
            ApplyValid(sectionNode, configPath);
        }
        catch (ConfigurationErrorsException exception)
        {
            _error = exception;
        }
    }

    internal DefaultDocuments Build(out ConfigurationErrorsException? error)
    {
        error = _error;
        return new DefaultDocuments(_enabled, _files.ToArray());
    }

    private void ApplyValid(XmlNode sectionNode, string configPath)
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
                        + " answers 500.19 for every directory request. <remove> it first to"
                        + " reposition it.");
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
