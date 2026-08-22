#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Web.Compilation;

namespace System.Web.IisConfig;

// The merged system.webServer/modules snapshot is the pipeline's module authority, fed to
// HttpApplication's integrated-mode collection builder. Types resolve here rather than at
// activation because IIS resolved them when it built the pipeline, after App_Code compiled
// (MH22).
internal static class IntegratedModules
{
    internal static List<ModuleConfigurationInfo> ConfigInfo(
        IReadOnlyList<IisRegistration> registrations,
        ManagedHandlerModules conditioned)
    {
        var modules = new List<ModuleConfigurationInfo>(registrations.Count);

        foreach (var registration in registrations)
        {
            var typeName = IisTypeIdentities.Retarget(RequireType(registration));
            RequireModuleType(registration, typeName);

            if (registration.RequiresManagedHandler)
            {
                conditioned.Add(registration.Name);
            }

            modules.Add(new ModuleConfigurationInfo(
                registration.Name,
                typeName,
                registration.RequiresManagedHandler ? HttpApplication.MANAGED_PRECONDITION : string.Empty));
        }

        return modules;
    }

    private static string RequireType(IisRegistration registration)
    {
        var typeName = registration.Type;
        if (string.IsNullOrEmpty(typeName))
        {
            throw new ConfigurationErrorsException(
                Refusal(registration.Name, "<none>") + " The entry carries no type= attribute.");
        }

        return typeName!;
    }

    private static void RequireModuleType(IisRegistration registration, string typeName)
    {
        Type? type;
        try
        {
            type = BuildManager.GetType(typeName, true /*throwOnError*/);
        }
        catch (Exception failure)
        {
            throw new ConfigurationErrorsException(
                Refusal(registration.Name, registration.Type!) + " " + failure.Message, failure);
        }

        if (!typeof(IHttpModule).IsAssignableFrom(type))
        {
            throw new ConfigurationErrorsException(
                Refusal(registration.Name, registration.Type!)
                + " The type does not implement System.Web.IHttpModule.");
        }
    }

    private static string Refusal(string name, string typeName) =>
        "<add name=\"" + name + "\"> in <system.webServer><modules> cannot be loaded, so the"
        + " application has no pipeline and can serve no request, static files included."
        + " Configured type: '" + typeName + "'.";
}
