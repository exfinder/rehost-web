#nullable enable

using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Rehost.Web;

internal static class AssemblyRetargets
{
    private static readonly FrozenDictionary<string, string> Names =
        new Dictionary<string, string>
        {
            ["System.Web"] = "Rehost.Web",
            ["System.Web.Extensions"] = "Rehost.Web.Extensions",
            ["System.Web.Services"] = "Rehost.Web.Services",
            ["System.Web.ApplicationServices"] = "Rehost.Web.ApplicationServices",
            ["System.Web.WebPages"] = "Rehost.Web.WebPages",
            ["System.Web.WebPages.Razor"] = "Rehost.Web.WebPages.Razor",
            ["System.Web.WebPages.Deployment"] = "Rehost.Web.WebPages.Deployment",
            ["System.Web.Mvc"] = "Rehost.Web.Mvc",
            ["System.Web.Http.WebHost"] = "Rehost.Web.Http.WebHost",
            ["System.Web.Optimization"] = "Rehost.Web.Optimization",
            ["Microsoft.AspNet.Web.Optimization.WebForms"] = "Rehost.AspNet.Web.Optimization.WebForms",
            ["Microsoft.Web.Infrastructure"] = "Rehost.Web.Infrastructure",
            ["Microsoft.Owin.Host.SystemWeb"] = "Rehost.Owin.Host.SystemWeb",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    internal static string Retarget(string assemblyQualifiedTypeName)
    {
        var separator = AssemblySeparator(assemblyQualifiedTypeName);
        if (separator < 0)
        {
            return assemblyQualifiedTypeName;
        }

        var replacement = Replacement(assemblyQualifiedTypeName.Substring(separator + 1));
        return replacement == null
            ? assemblyQualifiedTypeName
            : $"{assemblyQualifiedTypeName.Substring(0, separator)}, {replacement}";
    }

    internal static string RetargetAssemblyName(string assemblyName) =>
        Replacement(assemblyName) ?? assemblyName;

    private static string? Replacement(string assemblyName)
    {
        var end = assemblyName.IndexOf(',');
        var simpleName = (end < 0 ? assemblyName : assemblyName.Substring(0, end)).Trim();
        return Names.TryGetValue(simpleName, out var replacement) ? replacement : null;
    }

    private static int AssemblySeparator(string typeName)
    {
        var depth = 0;
        for (var index = 0; index < typeName.Length; index++)
        {
            var character = typeName[index];
            if (character == '[')
            {
                depth++;
            }
            else if (character == ']')
            {
                depth--;
            }
            else if (character == ',' && depth == 0)
            {
                return index;
            }
        }

        return -1;
    }
}
