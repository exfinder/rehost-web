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
    internal static void Apply(
        XmlNode sectionNode,
        IisCollectionSchema schema,
        Dictionary<string, string> entries,
        string configPath)
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

                entries.Add(
                    key,
                    schema.ValueAttribute == null
                        ? ""
                        : RequireAttribute(node, schema.ValueAttribute, configPath));
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
