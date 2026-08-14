using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Candidate probing is case-insensitive on every platform (ledger P67 decision 8): the page
// fixture's Default.aspx must answer the list's lowercase default.aspx entry on a filesystem
// where those are different names. Without the P57 fold every candidate misses and the
// directory request answers 403 instead.
public sealed class DefaultDocumentCaseOverKestrelTests(CaseSensitiveLiveScenario scenario)
{
    private LiveScenario RequireLive()
    {
        Assert.SkipWhen(
            scenario.Live == null, "No case-sensitive filesystem is available on this platform.");
        return scenario.Live!;
    }

    [Fact]
    public async Task The_Root_Default_Document_Serves_On_A_Case_Sensitive_Filesystem()
    {
        var live = RequireLive();

        var response = await live.Client.GetAsync("/");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task A_Differently_Cased_Candidate_Still_Serves()
    {
        var live = RequireLive();
        var directory = Path.Combine(live.ApplicationPath, "CasedSub");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "INDEX.html"), "cased-candidate");

        var response = await live.Client.GetAsync("/CasedSub/");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("cased-candidate");
    }
}
