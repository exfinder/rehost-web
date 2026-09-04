using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// IV10, IV18: integrated refuses a += or -= raised after module initialization, where classic
// accepted the binding and never ran the handler.
public sealed class LateEventBindingOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task Binding_An_Application_Event_From_BeginRequest_Is_Refused()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/Default.aspx?bind=1");

        response.StatusCode.ShouldBe(200);
        stages.ShouldContain(
            "bind:InvalidOperationException:Event handlers can only be bound to HttpApplication"
            + " events during IHttpModule initialization.");
        stages.ShouldContain(
            "unbind:InvalidOperationException:Event handlers can only be bound to HttpApplication"
            + " events during IHttpModule initialization.");
        stages.ShouldContain(
            "wrap-late:InvalidOperationException:Method OnExecuteRequestStep can only be called"
            + " during HttpApplication initialization or IHttpModule initialization.");
    }
}
