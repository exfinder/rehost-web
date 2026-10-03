#nullable enable

namespace System.Web.IisConfig;

// Registration rows transcribed from the IIS golden carry Framework assembly identities. An
// unqualified name needs no rewrite: BuildManager.GetType tries this assembly first and the
// application's top-level assemblies next, which is the order Framework resolved against
// System.Web and App_Code.
internal static class IisTypeIdentities
{
    internal static string Retarget(string typeName) => Rehost.Web.AssemblyRetargets.Retarget(typeName);
}
