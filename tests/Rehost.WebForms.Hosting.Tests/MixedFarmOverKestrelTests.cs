using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// A postback captured from a .NET Framework 4.8.1 node, replayed here over a real socket. Passing
// means a load-balanced farm may span both runtimes, so an application can be migrated a node at a
// time rather than all at once. The payload and its provenance ship with the fixture.
public sealed class MixedFarmOverKestrelTests(FarmBatch scenario) : IClassFixture<FarmBatch>
{
    private BatchRun Run => scenario.Run;

    [Fact]
    public void A_Framework_Postback_Raises_The_Control_Event_On_This_Runtime()
    {
        Run.Trace.ShouldContain("request:captured:postback:200");

        // clicked: the button's Click handler ran, so the payload survived MAC validation, view
        // state deserialization, and event validation rather than merely decrypting.
        Run.ResponseText("captured:postback")
            .ShouldContain("postback=True|carried=carried-from-initial|echo=clicked");
    }

    // Carried is assigned only when IsPostBack is false, so its value here crossed runtimes inside
    // the captured view state. Without this the test above would also pass on a payload that
    // validated but restored nothing.
    [Fact]
    public void Framework_Serialized_View_State_Restores_Control_Properties_Here()
    {
        var render = Run.ResponseText("captured:render");
        var postback = Run.ResponseText("captured:postback");

        render.ShouldContain("postback=False");
        postback.ShouldContain("carried=carried-from-initial");
    }

    // The control for both claims above: with the field dropped the same payload is refused, so
    // event validation was being enforced and the acceptance was not vacuous.
    [Fact]
    public void The_Same_Payload_Without_Event_Validation_Is_Refused()
    {
        Run.Trace.ShouldContain("request:captured-without-event-validation:postback:500");
        Run.ResponseText("captured-without-event-validation:postback")
            .ShouldContain("Invalid postback or callback argument");
    }
}
