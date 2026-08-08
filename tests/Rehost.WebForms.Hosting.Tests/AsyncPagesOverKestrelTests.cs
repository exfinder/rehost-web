using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Async="true" pages over the shared page host. The expected sc/ctx values per stage are a
// win-oracle 4.8.1 reading of these exact fixture pages (2026-08): HttpContext.Current is
// restored on every sync-context resume, null after ConfigureAwait(false), and restored again
// by render. Thread identity is scheduler-dependent and deliberately not asserted.
public sealed class AsyncPagesOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task A_Pending_PageAsyncTask_Resumes_With_The_Request_Context_Restored()
    {
        var response = await scenario.Client.GetAsync("/async/PendingTask.aspx");

        response.StatusCode.ShouldBe(200);
        var stages = ParseStages(response.Text);
        stages["load"].ShouldBe(("AspNetSynchronizationContext", "same"));
        stages["task-begin"].ShouldBe(("AspNetSynchronizationContext", "same"));
        stages["after-await"].ShouldBe(("AspNetSynchronizationContext", "same"));
        stages["after-caf"].ShouldBe(("null", "null"));
        stages["render"].ShouldBe(("AspNetSynchronizationContext", "same"));
    }

    [Fact]
    public async Task An_Async_Void_Handler_Resumes_With_The_Request_Context_Restored()
    {
        var response = await scenario.Client.GetAsync("/async/VoidHandler.aspx?k=restore");

        response.StatusCode.ShouldBe(200);
        var stages = ParseStages(response.Text);
        stages["load"].ShouldBe(("AspNetSynchronizationContext", "same"));
        stages["after-await"].ShouldBe(("AspNetSynchronizationContext", "same"));
        stages["after-caf"].ShouldBe(("null", "null"));
        stages["render"].ShouldBe(("AspNetSynchronizationContext", "same"));
    }

    [Fact]
    public async Task An_Apm_Task_Ends_And_Renders_With_The_Context_Restored()
    {
        var response = await scenario.Client.GetAsync("/async/ApmTask.aspx");

        response.StatusCode.ShouldBe(200);
        var stages = ParseStages(response.Text);
        stages["begin"].ShouldBe(("AspNetSynchronizationContext", "same"));
        stages["end"].ShouldBe(("AspNetSynchronizationContext", "same"));
        stages["render"].ShouldBe(("AspNetSynchronizationContext", "same"));
    }

    [Fact]
    public async Task AddOnPreRenderCompleteAsync_Ends_With_The_Context_Restored()
    {
        var response = await scenario.Client.GetAsync("/async/PreRenderCplt.aspx");

        response.StatusCode.ShouldBe(200);
        var stages = ParseStages(response.Text);
        stages["begin"].ShouldBe(("AspNetSynchronizationContext", "same"));
        stages["end"].ShouldBe(("AspNetSynchronizationContext", "same"));
        stages["render"].ShouldBe(("AspNetSynchronizationContext", "same"));
    }

    [Fact]
    public async Task ExecuteInParallel_Is_Refused_Under_The_TaskFriendly_Context()
    {
        var response = await scenario.Client.GetAsync("/async/ParallelTasks.aspx");

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("'executeInParallel'");
        response.Text.ShouldContain("unsupported in the current application configuration");
    }

    [Fact]
    public async Task Sequential_Apm_Tasks_Run_In_Registration_Order()
    {
        var response = await scenario.Client.GetAsync("/async/ParallelTasks.aspx?seq=1");

        response.StatusCode.ShouldBe(200);
        response.Text.Trim().ShouldBe("a-begin|a-end|b-begin|b-end");
    }

    [Fact]
    public async Task A_CancellationToken_Task_Sees_A_Cancellable_Untripped_Token()
    {
        var response = await scenario.Client.GetAsync("/async/TokenTask.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.Trim().ShouldBe("start[canRequest=True]|done[cancelled=False]");
    }

    [Fact]
    public async Task A_Task_Exceeding_AsyncTimeout_Fails_With_The_Page_Timeout_Error()
    {
        var response = await scenario.Client.GetAsync("/async/TokenTask.aspx?slow=1");

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("An asynchronous operation exceeded the page timeout.");
    }

    [Fact]
    public async Task Response_End_Inside_A_Task_Suppresses_The_Tail()
    {
        var response = await scenario.Client.GetAsync("/async/EndInTask.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("before-end|");
    }

    [Fact]
    public async Task An_Async_Void_Handler_Finishes_Before_Render_With_No_Stragglers()
    {
        var first = await scenario.Client.GetAsync("/async/VoidHandler.aspx?k=complete");
        var dump = await scenario.Client.GetAsync("/async/VoidHandler.aspx?dump=complete");

        dump.Text.Trim().ShouldBe(first.Text.Trim());
    }

    private static Dictionary<string, (string Sc, string Ctx)> ParseStages(string body)
    {
        var stages = new Dictionary<string, (string, string)>();
        foreach (Match match in Regex.Matches(
            body.Trim(),
            @"([a-z-]+)\[tid=(?:load|other);sc=([A-Za-z]+);ctx=([a-z]+)\]"))
        {
            stages[match.Groups[1].Value] = (match.Groups[2].Value, match.Groups[3].Value);
        }

        return stages;
    }
}
