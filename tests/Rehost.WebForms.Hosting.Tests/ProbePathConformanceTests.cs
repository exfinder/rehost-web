using System.Xml.Linq;
using Rehost.WebForms.ScenarioProtocol;
using Rehost.WebForms.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

public sealed class ProbePathConformanceTests
{
    private static readonly Dictionary<string, string> HandlerPaths = new()
    {
        ["WitnessHandler"] = ProbePaths.Witness,
        ["ResponseHeadersProbe"] = ProbePaths.Headers,
        ["RequestBodyHandler"] = ProbePaths.Body,
        ["CookieHandler"] = ProbePaths.Cookies,
        ["SaveHandler"] = ProbePaths.Save,
        ["WebSocketProbe"] = ProbePaths.WebSocketEcho,
        ["StreamingProbe"] = ProbePaths.StreamFlush,
        ["AsyncStreamingProbe"] = ProbePaths.StreamAsyncFlush,
        ["SessionProbe"] = ProbePaths.Session,
        ["PlainSessionProbe"] = ProbePaths.SessionPlain,
        ["ReadOnlySessionProbe"] = ProbePaths.SessionReadOnly,
        ["QuirksHandler"] = ProbePaths.Quirks,
        ["AuthProbe"] = ProbePaths.AuthStatus,
        ["SignInProbe"] = ProbePaths.AuthSignIn,
        ["ProfileWriteProbe"] = ProbePaths.AuthProfile,
        ["ProtectedProbe"] = ProbePaths.Secret,
        ["ScenarioHandler"] = ProbePaths.ScenarioDefault,
    };

    // The handler-walk markers report which row of the merged list won, so the path they sit at
    // is the variable under test and no single one can be typed for them.
    private static readonly HashSet<string> PathIsUnderTest = new()
    {
        "MarkerAHandler",
        "MarkerBHandler",
    };

    [Fact]
    public void Every_Fixture_Registers_Probe_Handlers_At_Their_Typed_Paths()
    {
        var fixturesRoot = Path.Combine(ScenarioHostInvocation.HostDirectory, "fixtures");
        var seen = 0;

        foreach (var fixture in Directory.GetDirectories(fixturesRoot))
        {
            var config = Path.Combine(fixture, "web.config");
            if (!File.Exists(config))
            {
                continue;
            }

            foreach (var add in XDocument.Load(config).Descendants("add"))
            {
                var type = (string?)add.Attribute("type");
                var path = (string?)add.Attribute("path");
                if (path == null
                    || type == null
                    || !type.Contains("Rehost.WebForms.ScenarioProbes."))
                {
                    continue;
                }

                var handler = type.Split(',')[0].Split('.')[^1];
                if (PathIsUnderTest.Contains(handler))
                {
                    continue;
                }

                seen++;
                HandlerPaths.ShouldContainKey(
                    handler,
                    $"{Path.GetFileName(fixture)} registers a probe handler with no typed path");
                ("/" + path).ShouldBe(
                    HandlerPaths[handler],
                    $"{Path.GetFileName(fixture)} registers {handler} off its typed path");
            }
        }

        seen.ShouldBeGreaterThan(20);
    }
}
