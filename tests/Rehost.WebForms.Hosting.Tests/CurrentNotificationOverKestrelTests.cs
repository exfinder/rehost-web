using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// IV6: the notification and post-phase flag every managed event reports on an integrated pool.
// The port answers them from the classic step list, which has no notification context of its own.
public sealed class CurrentNotificationOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task Every_Managed_Event_Reports_Its_Integrated_Notification()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/Default.aspx?cn=1");

        response.StatusCode.ShouldBe(200);
        stages.Where(stage => stage.StartsWith("cn|", StringComparison.Ordinal)).ShouldBe(
        [
            "cn|BeginRequest=BeginRequest/False",
            "cn|AuthenticateRequest=AuthenticateRequest/False",
            "cn|PostAuthenticateRequest=AuthenticateRequest/True",
            "cn|AuthorizeRequest=AuthorizeRequest/False",
            "cn|PostAuthorizeRequest=AuthorizeRequest/True",
            "cn|ResolveRequestCache=ResolveRequestCache/False",
            "cn|PostResolveRequestCache=ResolveRequestCache/True",
            "cn|MapRequestHandler=MapRequestHandler/False",
            "cn|PostMapRequestHandler=MapRequestHandler/True",
            "cn|AcquireRequestState=AcquireRequestState/False",
            "cn|PostAcquireRequestState=AcquireRequestState/True",
            "cn|PreRequestHandlerExecute=PreExecuteRequestHandler/False",
            "cn|handler=ExecuteRequestHandler/False",
            "cn|PostRequestHandlerExecute=ExecuteRequestHandler/True",
            "cn|ReleaseRequestState=ReleaseRequestState/False",
            "cn|PostReleaseRequestState=ReleaseRequestState/True",
            "cn|UpdateRequestCache=UpdateRequestCache/False",
            "cn|PostUpdateRequestCache=UpdateRequestCache/True",
            "cn|LogRequest=LogRequest/False",
            "cn|PostLogRequest=LogRequest/True",
            "cn|EndRequest=EndRequest/False",
            "cn|PreSendRequestHeaders=SendResponse/False",
            "cn|PreSendRequestContent=SendResponse/False",
        ]);
    }

    // IV12: integrated supplies HttpContext.Current inside both pre-send events; classic, which
    // raises them from the flush, leaves it null.
    [Fact]
    public async Task The_Pre_Send_Events_Run_With_A_Current_Context()
    {
        var (_, stages) = await scenario.TracedGetAsync(this, "/Default.aspx?cn=1");

        stages.ShouldContain("PreSendRequestHeaders:set");
        stages.ShouldContain("PreSendRequestContent:set");
    }
}
