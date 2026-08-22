using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// mode="Custom" resolved from a configured type name, and the call sequence the module drives it
// with. A store that round-tripped values while ignoring the locking protocol would satisfy the
// value assertions alone, so the recorded sequence is what carries the claim.
public sealed class CustomSessionStoreOverKestrelTests(SessionCustomLiveScenario scenario)
    : IClassFixture<SessionCustomLiveScenario>
{
    private const string CookieName = "ASP.NET_SessionId";

    [Fact]
    public async Task A_Configured_Provider_Serves_The_Session_And_Is_Driven_Exclusively()
    {
        var written = await scenario.Client.GetAsync(ProbePaths.Session + "?mode=write&v=custom");
        var cookie = SessionCookie(written);

        var read = await scenario.Client.GetWithCookiesAsync(ProbePaths.Session, cookie);
        Field(read, "v").ShouldBe("custom");
        Field(read, "mode").ShouldBe("Custom");

        var calls = await StoreCallsAsync();
        calls.ShouldContain("GetItemExclusive");
        calls.ShouldContain("SetAndReleaseItemExclusive");
    }

    [Fact]
    public async Task A_Read_Only_Handler_Takes_The_Shared_Acquire()
    {
        await scenario.Client.GetAsync(ProbePaths.Session + "?mode=write&v=shared");
        var cookie = SessionCookie(await scenario.Client.GetAsync(ProbePaths.Session + "?mode=write&v=shared"));

        var before = (await StoreCallsAsync()).Count;
        await scenario.Client.GetWithCookiesAsync(ProbePaths.SessionReadOnly, cookie);
        var added = (await StoreCallsAsync()).Skip(before).ToArray();

        added.ShouldContain("GetItem");
        added.ShouldNotContain("GetItemExclusive");
    }

    private async Task<List<string>> StoreCallsAsync() =>
        [.. (await scenario.Witness.EventsAsync())
            .Where(entry => entry.StartsWith(WitnessProtocol.SessionStoreCall, StringComparison.Ordinal))
            .Select(entry => entry[WitnessProtocol.SessionStoreCall.Length..])];

    private static string SessionCookie(ScenarioResponse response) =>
        response.SetCookies
            .Single(header => header.StartsWith(CookieName + "=", StringComparison.Ordinal))
            .Split(';')[0];

    private static string Field(ScenarioResponse response, string name) =>
        response.Text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith(name + "=", StringComparison.Ordinal))[(name.Length + 1)..];
}
