using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// InProc session state over the shipped root module registration. The fixture is its own host
// because its Global.asax declares Session_Start, which promotes every request in the
// application to a stored session and a Set-Cookie (reading S15).
public sealed class SessionStateOverKestrelTests(SessionLiveScenario scenario)
    : IClassFixture<SessionLiveScenario>
{
    private const string CookieName = "ASP.NET_SessionId";
    private const int HoldMilliseconds = 1500;

    [Fact]
    public async Task A_Written_Value_Round_Trips_Under_The_Issued_Cookie()
    {
        var written = await scenario.Client.GetAsync(ProbePaths.Session + "?mode=write&v=hello");

        written.StatusCode.ShouldBe(200);
        var cookie = SessionCookie(written);
        Field(written, "isnew").ShouldBe("True");

        var read = await scenario.Client.GetWithCookiesAsync(ProbePaths.Session, cookie);

        Field(read, "id").ShouldBe(Field(written, "id"));
        Field(read, "isnew").ShouldBe("False");
        Field(read, "v").ShouldBe("hello");
        read.SetCookies.ShouldBeEmpty();
    }

    [Fact]
    public async Task Session_Identity_And_Handler_Opt_In_Behave_As_Framework_Does()
    {
        var first = await scenario.Client.GetAsync(ProbePaths.Session + "?mode=write&v=a");
        var cookie = SessionCookie(first);

        var second = await scenario.Client.GetWithCookiesAsync(ProbePaths.Session, cookie);
        Field(second, "id").ShouldBe(Field(first, "id"));
        Field(second, "starts").ShouldBe("1");

        var plain = await scenario.Client.GetWithCookiesAsync(ProbePaths.SessionPlain, cookie);
        Field(plain, "session").ShouldBe("null");

        var readOnly = await scenario.Client.GetWithCookiesAsync(ProbePaths.SessionReadOnly, cookie);
        Field(readOnly, "session").ShouldBe("present");
        Field(readOnly, "readonly").ShouldBe("True");
        Field(readOnly, "id").ShouldBe(Field(first, "id"));

        var writable = await scenario.Client.GetWithCookiesAsync(ProbePaths.Session, cookie);
        Field(writable, "readonly").ShouldBe("False");
        Field(writable, "mode").ShouldBe("InProc");
        Field(writable, "cookieless").ShouldBe("False");
        Field(writable, "timeout").ShouldBe("20");
    }

    // The directive is delivered by codegen emitting IReadOnlySessionState on the generated page
    // class. InProc hands out a live reference to the cached collection, so the write sticks
    // (reading S23) where an out-of-process store would discard it.
    [Fact]
    public async Task A_Read_Only_Page_Directive_Reads_Without_Locking_And_Its_Write_Persists()
    {
        var seeded = await scenario.Client.GetAsync(ProbePaths.Session + "?mode=write&v=seed");
        var cookie = SessionCookie(seeded);

        var page = await scenario.Client.GetWithCookiesAsync("/ReadOnly.aspx?write=from-readonly", cookie);

        page.StatusCode.ShouldBe(200);
        page.Text.ShouldContain("readonly=True", Case.Sensitive);
        page.Text.ShouldContain("v=seed");
        page.Text.ShouldContain("write=ok");

        var after = await scenario.Client.GetWithCookiesAsync(ProbePaths.Session, cookie);
        Field(after, "v").ShouldBe("from-readonly");
    }

    [Fact]
    public async Task Abandon_Empties_The_Session_And_Raises_Session_End()
    {
        var seeded = await scenario.Client.GetAsync(ProbePaths.Session + "?mode=write&v=doomed");
        var cookie = SessionCookie(seeded);
        var id = Field(seeded, "id");

        await scenario.Client.GetWithCookiesAsync(ProbePaths.Session + "?mode=abandon", cookie);

        var ended = await scenario.Witness.WaitForAsync(
            WitnessProtocol.SessionEnded + id,
            TimeSpan.FromSeconds(10));
        ended.ShouldContain("context=null");

        var after = await scenario.Client.GetWithCookiesAsync(ProbePaths.Session, cookie);
        Field(after, "id").ShouldBe(id);
        Field(after, "isnew").ShouldBe("True");
        Field(after, "v").ShouldBe("null");
    }

    // The handshake is what keeps this from passing vacuously: the second request is dispatched
    // only after the first reports itself inside the lock, so "B ran after A" cannot be an
    // accident of scheduling. The paired overlap case rules out a merely serial host; the test
    // holds P at a gate until it has seen Q inside, so a slow Q cannot fail it, only a blocked one.
    [Fact]
    public async Task Two_Requests_Sharing_A_Session_Serialize_While_Separate_Sessions_Overlap()
    {
        var first = await scenario.Client.GetAsync(ProbePaths.Session + "?mode=write&v=x");
        var shared = SessionCookie(first);

        var held = Dispatch(shared, "A");
        await scenario.Witness.WaitForAsync(
            WitnessProtocol.SessionEntered + "A",
            TimeSpan.FromSeconds(10));
        var blocked = Dispatch(shared, "B");
        await Task.WhenAll(held, blocked);

        var serialized = await SessionMarkersAsync();
        serialized.IndexOf(WitnessProtocol.SessionEntered + "B")
            .ShouldBeGreaterThan(serialized.IndexOf(WitnessProtocol.SessionExited + "A"));

        var other = await scenario.Client.GetAsync(ProbePaths.Session + "?mode=write&v=y");
        var separate = SessionCookie(other);
        SessionIdOf(separate).ShouldNotBe(SessionIdOf(shared));

        using var gate = new ScenarioGate();
        var one = Dispatch(shared, "P", gate);
        await gate.WaitForArrivalAsync(TestContext.Current.CancellationToken);
        var two = Dispatch(separate, "Q");
        await scenario.Witness.WaitForAsync(
            WitnessProtocol.SessionEntered + "Q",
            ScenarioGate.DefaultWait);
        gate.Release();
        await Task.WhenAll(one, two);

        var overlapped = await SessionMarkersAsync();
        overlapped.IndexOf(WitnessProtocol.SessionEntered + "Q")
            .ShouldBeLessThan(overlapped.IndexOf(WitnessProtocol.SessionExited + "P"));
    }

    private Task<ScenarioResponse> Dispatch(string cookie, string tag, ScenarioGate? gate = null) =>
        scenario.Client.GetWithCookiesAsync(
            gate == null
                ? $"{ProbePaths.Session}?mode=hold&ms={HoldMilliseconds}&tag={tag}"
                : $"{ProbePaths.Session}?mode=hold&tag={tag}&gate={gate.Name}",
            cookie);

    private async Task<List<string>> SessionMarkersAsync() =>
        [.. (await scenario.Witness.EventsAsync()).Where(
            entry => entry.StartsWith(WitnessProtocol.SessionEntered, StringComparison.Ordinal)
                || entry.StartsWith(WitnessProtocol.SessionExited, StringComparison.Ordinal))];

    private static string SessionCookie(ScenarioResponse response) =>
        response.SetCookies
            .Single(header => header.StartsWith(CookieName + "=", StringComparison.Ordinal))
            .Split(';')[0];

    private static string SessionIdOf(string cookie) => cookie[(CookieName.Length + 1)..];

    private static string Field(ScenarioResponse response, string name) =>
        response.Text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith(name + "=", StringComparison.Ordinal))[(name.Length + 1)..];
}
