#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace System.Web.IisConfig;

// IIS's collection shape with its measured semantics (readings C1-C3): duplicate add errors,
// remove of an absent key is tolerated, clear empties the inherited entries.
internal sealed class IisCollectionSchema
{
    internal IisCollectionSchema(string addElement, string keyAttribute, string? valueAttribute)
    {
        AddElement = addElement;
        KeyAttribute = keyAttribute;
        ValueAttribute = valueAttribute;
    }

    internal string AddElement { get; }

    internal string KeyAttribute { get; }

    // Null for membership-only collections (hiddenSegments); required when present (mimeMap).
    internal string? ValueAttribute { get; }
}

internal static class IisCollectionReader
{
    internal const string BooleanRule =
        """is not a boolean; IIS accepts only "true" or "false".""";

    // IIS types these attributes as bool in IIS_schema.xml and refuses anything else with a
    // 500.19 naming the attribute (MH31), so an unparsable value cannot be treated as absent.
    internal static bool? OptionalBoolean(
        XmlNode? node, string elementName, string attribute, string configPath)
    {
        var value = node?.Attributes?[attribute]?.Value;
        return value == null
            ? null
            : Boolean($"""<{elementName} {attribute}="{value}">""", value, configPath);
    }

    internal static bool Boolean(string element, string value, string configPath)
    {
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new ConfigurationErrorsException($"{element} in '{configPath}' {BooleanRule}");
    }

    internal static void Apply(
        XmlNode sectionNode,
        IisCollectionSchema schema,
        IDictionary<string, string> entries,
        string configPath) =>
        Apply(
            sectionNode,
            schema,
            entries,
            configPath,
            (node, path) => schema.ValueAttribute == null
                ? ""
                : RequireAttribute(node, schema.ValueAttribute, path));

    internal static void Apply<TValue>(
        XmlNode sectionNode,
        IisCollectionSchema schema,
        IDictionary<string, TValue> entries,
        string configPath,
        Func<XmlNode, string, TValue> readValue)
    {
        foreach (XmlNode node in sectionNode.ChildNodes)
        {
            if (node.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (node.Name == "clear")
            {
                entries.Clear();
            }
            else if (node.Name == "remove")
            {
                entries.Remove(RequireAttribute(node, schema.KeyAttribute, configPath));
            }
            else if (node.Name == schema.AddElement)
            {
                var key = RequireAttribute(node, schema.KeyAttribute, configPath);
                if (entries.ContainsKey(key))
                {
                    throw new ConfigurationErrorsException(
                        "<" + schema.AddElement + " " + schema.KeyAttribute + "=\"" + key
                        + "\"> in '" + configPath + "' duplicates an entry the collection"
                        + " already contains; IIS refuses this with a 500.19. <remove> it"
                        + " first to change it.");
                }

                entries.Add(key, readValue(node, configPath));
            }
            else
            {
                throw new ConfigurationErrorsException(
                    "'" + configPath + "' contains unsupported element <" + node.Name
                    + "> inside <" + sectionNode.Name + ">.");
            }
        }
    }

    internal static string RequireAttribute(XmlNode node, string name, string configPath)
    {
        var value = node.Attributes?[name]?.Value;
        if (string.IsNullOrEmpty(value))
        {
            throw new ConfigurationErrorsException(
                "<" + node.Name + "> in '" + configPath + "' is missing its '" + name
                + "' attribute.");
        }

        return value;
    }
}
