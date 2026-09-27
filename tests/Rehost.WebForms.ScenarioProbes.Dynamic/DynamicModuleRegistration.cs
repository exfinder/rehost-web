using System.Web;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Rehost.WebForms.ScenarioProbes;
using Rehost.WebForms.ScenarioProbes.Dynamic;

[assembly: PreApplicationStartMethod(
    typeof(DynamicModuleRegistration),
    nameof(DynamicModuleRegistration.Start))]

namespace Rehost.WebForms.ScenarioProbes.Dynamic;

// Its own bin payload, so only the modules fixtures carry dynamically registered modules.
public static class DynamicModuleRegistration
{
    public static void Start()
    {
        HttpApplication.RegisterModule(typeof(DynamicModuleProbe));
        DynamicModuleUtility.RegisterModule(typeof(DynamicModuleUtilityProbe));
    }
}
