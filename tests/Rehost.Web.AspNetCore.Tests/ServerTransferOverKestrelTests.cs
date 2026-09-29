using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// Server.Transfer and Server.Execute over the shared page host. Transfer rides the End
// termination machinery (ledger P52), so the parent's earlier writes survive and its tail is
// suppressed; Execute composes the child inline and always takes the IHttpAsyncHandler arm.
// The static-file arm crosses the P61 known-physical seam, and TransferRequest pins the P62
// refusal.
public sealed class ServerTransferOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task Transfer_Renders_The_Child_And_Suppresses_The_Parent_Tail()
    {
        var response = await scenario.Client.GetAsync("/xfer/Parent.aspx?m=t");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe(
            "parent-before|child[q=m=t;prev=~/xfer/Parent.aspx"
            + ";cur=/xfer/Child.aspx;fp=/xfer/Parent.aspx]");
    }

    [Fact]
    public async Task Transfer_Honors_A_Query_Override_And_PreserveForm_False()
    {
        var overridden = await scenario.Client.GetAsync("/xfer/Parent.aspx?m=tq");
        var cleared = await scenario.Client.GetAsync("/xfer/Parent.aspx?m=tf");

        overridden.Text.ShouldContain("child[q=from=override;");
        cleared.Text.ShouldContain("child[q=;");
    }

    [Fact]
    public async Task Transfer_Accepts_A_Compiled_Page_Handler()
    {
        var response = await scenario.Client.GetAsync("/xfer/Parent.aspx?m=th");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe(
            "parent-before|child[q=;prev=~/xfer/Parent.aspx"
            + ";cur=/xfer/Parent.aspx;fp=/xfer/Parent.aspx]");
    }

    [Fact]
    public async Task Execute_Composes_The_Child_Inline_And_The_Parent_Continues()
    {
        var response = await scenario.Client.GetAsync("/xfer/Parent.aspx?m=e");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe(
            "parent-before|child[q=m=e;prev=~/xfer/Parent.aspx"
            + ";cur=/xfer/Child.aspx;fp=/xfer/Parent.aspx]parent-after|");
    }

    [Fact]
    public async Task Execute_Captures_Into_A_Writer_Instead_Of_The_Response()
    {
        var response = await scenario.Client.GetAsync("/xfer/Parent.aspx?m=ew");

        response.Text.ShouldBe(
            "parent-before|captured[child[q=m=ew;prev=~/xfer/Parent.aspx"
            + ";cur=/xfer/Child.aspx;fp=/xfer/Parent.aspx]]parent-after|");
    }

    [Fact]
    public async Task Execute_Serves_A_Static_File_Through_The_Handler_Arm()
    {
        var response = await scenario.Client.GetAsync("/xfer/Parent.aspx?m=es");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("static-through-execute");
        response.Text.ShouldEndWith("parent-after|");
    }

    [Fact]
    public async Task An_Error_Transfer_Renders_The_Error_Page_With_GetLastError()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/xfer/Boom.aspx?xferr=1");

        response.Text.ShouldBe("error-page[last=HttpUnhandledException:boom-from-page]");
        stages.ShouldContain(s => s.StartsWith("ApplicationError:HttpUnhandledException", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TransferRequest_Is_Refused_With_An_Actionable_Message()
    {
        var response = await scenario.Client.GetAsync("/xfer/Parent.aspx?m=tr");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe(
            "parent-before|tr-caught:PlatformNotSupportedException:"
            + "Server.TransferRequest is not supported on this host: there is no IIS pipeline"
            + " to re-enter. Use Server.Transfer or Server.Execute instead.|parent-after|");
    }
}
