using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The adapter hands the built host's ILoggerFactory to the runtime before Kestrel accepts, so a
// request error lands in the host's log pipeline with the exception still an object (ADR 0011).
public sealed class RuntimeDiagnosticsOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task A_Failed_Request_Reaches_The_Host_Log_Pipeline_With_Its_Exception()
    {
        var failed = await scenario.Client.GetAsync("/async/ParallelTasks.aspx");
        var journal = await scenario.Client.GetAsync(HostLogProtocol.Path);

        failed.StatusCode.ShouldBe(500);
        var entry = HostLogProtocol.Parse(journal.Text)
            .FirstOrDefault(e => e.ExceptionText.Contains("executeInParallel", StringComparison.Ordinal))
            .ShouldNotBeNull();
        entry.Category.ShouldBe("Rehost.WebForms.Runtime");
        entry.Level.ShouldBe("Error");
        entry.EventId.ShouldBe(8);
        entry.ExceptionType.ShouldBe("System.Web.HttpUnhandledException");
        entry.Message.ShouldBe("unhandled request error from System.Web.HttpResponse");
    }
}
