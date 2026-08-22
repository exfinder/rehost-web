using System.Web;
using Rehost.WebForms.ScenarioProbes;
using Rehost.WebForms.ScenarioProbes.Dynamic;

[assembly: PreApplicationStartMethod(
    typeof(DynamicModuleRegistration),
    nameof(DynamicModuleRegistration.Start))]

namespace Rehost.WebForms.ScenarioProbes.Dynamic;

// The registration channel Optimization and OWIN use. It lives in its own bin payload so only the
// modules fixture carries a dynamically registered module.
public static class DynamicModuleRegistration
{
    public static void Start() => HttpApplication.RegisterModule(typeof(DynamicModuleProbe));
}
