using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// The AddOnMapRequestHandlerAsync / AddOnLogRequestAsync / AddOnPostLogRequestAsync overloads were
// refused off integrated (IV2); registering them in Init is what the fixture application does, so a
// refusal would take the whole host down rather than fail one request.
public sealed class AsyncPipelineEventFamilyOverKestrelTests(AsyncAppLiveScenario scenario)
    : IClassFixture<AsyncAppLiveScenario>
{
    [Fact]
    public async Task The_Async_Overloads_Run_Around_The_Handler()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/pending");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldStartWith("handler[");
        Index(stages, "AsyncMapRequestHandler").ShouldBeLessThan(Index(stages, "AsyncHandler"));
        Index(stages, "AsyncHandler").ShouldBeLessThan(Index(stages, "AsyncLogRequest"));
        Index(stages, "AsyncLogRequest").ShouldBeLessThan(Index(stages, "AsyncPostLogRequest"));
    }

    private static int Index(string[] stages, string stage)
    {
        var index = Array.IndexOf(stages, stage);
        return index >= 0
            ? index
            : throw new InvalidOperationException(
                "No stage matched among " + string.Join(", ", stages) + ".");
    }
}
