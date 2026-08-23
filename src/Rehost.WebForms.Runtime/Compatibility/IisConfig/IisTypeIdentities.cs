#nullable enable

namespace System.Web.IisConfig;

// Registration rows transcribed from the IIS golden carry Framework assembly identities. An
// unqualified name needs no rewrite: BuildManager.GetType tries this assembly first and the
// application's top-level assemblies next, which is the order Framework resolved against
// System.Web and App_Code.
internal static class IisTypeIdentities
{
    private const string FrameworkWebAssembly = "System.Web";
    private const string FrameworkExtensionsAssembly = "System.Web.Extensions";
    private const string FrameworkServicesAssembly = "System.Web.Services";
    private const string ExtensionsAssembly = "Rehost.WebForms.Extensions";
    private const string ServicesAssembly = "Rehost.WebForms.WebServices";

    internal static string Retarget(string typeName)
    {
        var separator = AssemblySeparator(typeName);
        if (separator < 0)
        {
            return typeName;
        }

        var simpleName = SimpleAssemblyName(typeName, separator + 1);

        string? replacement = null;
        if (string.Equals(simpleName, FrameworkWebAssembly, StringComparison.OrdinalIgnoreCase))
        {
            replacement = ModName.WEB_BASE_NAME;
        }
        else if (string.Equals(
            simpleName, FrameworkExtensionsAssembly, StringComparison.OrdinalIgnoreCase))
        {
            replacement = ExtensionsAssembly;
        }
        else if (string.Equals(
            simpleName, FrameworkServicesAssembly, StringComparison.OrdinalIgnoreCase))
        {
            replacement = ServicesAssembly;
        }

        return replacement == null
            ? typeName
            : typeName.Substring(0, separator) + ", " + replacement;
    }

    private static string SimpleAssemblyName(string typeName, int start)
    {
        var end = typeName.IndexOf(',', start);
        return (end < 0 ? typeName.Substring(start) : typeName.Substring(start, end - start)).Trim();
    }

    // A generic type argument list carries its own assembly-qualified names inside brackets.
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
