using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The AddOnMapRequestHandlerAsync / AddOnPostLogRequestAsync overloads were refused off
// integrated (IV2); registering them in Init is what the fixture application does, so a refusal
// would take the whole host down rather than fail one request.
public sealed class AsyncPipelineEventFamilyOverKestrelTests(AsyncAppLiveScenario scenario)
    : IClassFixture<AsyncAppLiveScenario>
{
    [Fact]
    public async Task The_Async_Overloads_Run_Around_The_Handler()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/pending");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldStartWith("handler[");
        Array.IndexOf(stages, "AsyncMapRequestHandler")
            .ShouldBeInRange(0, stages.Length - 1);
        Array.IndexOf(stages, "AsyncMapRequestHandler")
            .ShouldBeLessThan(Array.IndexOf(stages, "AsyncPostLogRequest"));
    }
}
