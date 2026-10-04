#nullable enable

namespace System.Web.Util;

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Xml;

internal static class ProbingPaths
{
    private const string AssemblyBindingNamespace = "urn:schemas-microsoft-com:asm.v1";

    internal static IReadOnlyList<string> Parse(
        string? runtimeRawXml,
        string applicationPhysicalRoot,
        string configPath)
    {
        if (String.IsNullOrEmpty(runtimeRawXml))
        {
            return [];
        }

        var document = new XmlDocument { XmlResolver = null };
        document.LoadXml(runtimeRawXml);
        var namespaces = new XmlNamespaceManager(document.NameTable);
        namespaces.AddNamespace("asm", AssemblyBindingNamespace);
        var attribute = (XmlAttribute?)(
            document.SelectSingleNode("/runtime/asm:assemblyBinding/asm:probing/@privatePath", namespaces)
            ?? document.SelectSingleNode("/runtime/assemblyBinding/probing/@privatePath"));
        if (attribute == null)
        {
            return [];
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationPhysicalRoot))
            + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var directories = new List<string>();
        var seen = new HashSet<string>(StringComparer.FromComparison(comparison));
        foreach (var entry in attribute.Value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var relative = entry.Replace('\\', '/');
            var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(root, relative)));
            if (Path.IsPathRooted(relative)
                || !(directory + Path.DirectorySeparatorChar).StartsWith(root, comparison))
            {
                throw new ConfigurationErrorsException(
                    $"""
                    <probing privatePath="{attribute.Value}"> in '{configPath}' lists '{entry}'. Probing paths are relative to the application root and must stay inside it.
                    """);
            }

            if (seen.Add(directory))
            {
                directories.Add(directory);
            }
        }

        return directories;
    }
}
