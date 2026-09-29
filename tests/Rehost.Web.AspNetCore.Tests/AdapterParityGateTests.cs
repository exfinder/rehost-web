using Rehost.Web.Parity.Harness;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// Activating an application permanently mutates process-global state, so every session runs in
// its own child process; orchestration and comparison stay in this process.
public sealed class AdapterParityGateTests
{
    private static readonly ParityGateRunner Gate = new("Rehost.Web.Parity.AdapterHost");

    public static TheoryData<string> Sessions
    {
        get
        {
            var sessions = new TheoryData<string>();
            foreach (var name in Gate.SessionNames)
            {
                sessions.Add(name);
            }

            return sessions;
        }
    }

    [Fact]
    public void Golden_Trace_Uses_The_Current_Schema()
    {
        Gate.GoldenSchemaVersion.ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(Sessions))]
    public void Session_Matches_The_Framework_Golden_Trace_Over_Kestrel(string session)
    {
        Gate.VerifySession(session, TraceComparison.Adapter);
    }
}
