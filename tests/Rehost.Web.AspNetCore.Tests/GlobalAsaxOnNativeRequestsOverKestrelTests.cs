using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// runAllManagedModulesForAllRequests lets Global.asax see a native request, as it did on IIS
// (MH41: LogRequest with the final status, no error event).
public sealed class GlobalAsaxOnNativeRequestsOverKestrelTests(ModulesRunAllLiveScenario scenario)
    : IClassFixture<ModulesRunAllLiveScenario>
{
    [Fact]
    public async Task RunAllManagedModules_Lets_Global_Asax_See_A_Missing_Static_File()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/nosuch.json?gax=1");

        response.StatusCode.ShouldBe(404);
        stages.ShouldContain("gax|LogRequest:404");
    }
}
