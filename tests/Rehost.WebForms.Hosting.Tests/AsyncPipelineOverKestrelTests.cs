using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The asyncapp fixture's pipeline-level async arms, pinned to a win-oracle 4.8.1 reading
// (2026-08): the awaited AddOnBeginRequestAsync module event resumes with HttpContext.Current
// restored, while EndProcessRequest of a truly-pending IHttpAsyncHandler runs on the completion
// side without it — Framework leaves it null there too. Thread identities are
// scheduler-dependent and not asserted.
public sealed class AsyncPipelineOverKestrelTests(AsyncAppLiveScenario scenario)
    : IClassFixture<AsyncAppLiveScenario>
{
    [Fact]
    public async Task A_Pending_Handler_Completes_With_The_Module_Await_Context_Restored()
    {
        var response = await scenario.Client.GetAsync("/pending");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldMatch(
            @"^handler\[tid=(?:begin|other);ctx=null;"
            + @"module=tid=(?:before|other);sc=AspNetSynchronizationContext;ctx=same\]$");
    }
}
