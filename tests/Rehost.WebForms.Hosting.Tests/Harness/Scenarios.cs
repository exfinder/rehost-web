using System.Runtime.CompilerServices;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.Hosting.Tests;

// Markers over the two host doors: classes keep IClassFixture wiring while every class on a
// shared fixture reaches the same child process. A registry-backed marker never disposes — the
// registry owns the host; an isolated marker owns its host and must dispose it. The base class
// is the witness grant: a marker exposes the whole-process witness only where cross-traffic is
// structural (session end and socket aborts have no request to carry a token) or the host is
// private to the consuming class.
public abstract class Scenario
{
    private protected Scenario(LiveScenario host) => Host = host;

    private protected LiveScenario Host { get; }

    internal string ApplicationPath => Host.ApplicationPath;

    internal int HostProcessId => Host.HostProcessId;

    internal Uri Address => Host.Address;

    internal ScenarioClient Client => Host.Client;
}

// A traced request is one module: token minting, the request, and the stage read cannot drift
// apart, and a stage array that comes back empty fails here rather than letting a negative
// assertion pass vacuously.
public abstract class WitnessScenario : Scenario
{
    private protected WitnessScenario(LiveScenario host)
        : base(host)
    {
    }

    internal async Task<(ScenarioResponse Response, string[] Stages)> TracedGetAsync(
        object testClass, string pathAndQuery, [CallerMemberName] string method = "")
    {
        var token = TracedToken.For(testClass, method);
        var response = await Client.GetAsync(TracedToken.Append(pathAndQuery, token));
        return (response, await StagesOrThrowAsync(token, pathAndQuery));
    }

    internal async Task<(byte[] Raw, string[] Stages)> TracedRawGetAsync(
        object testClass, string pathAndQuery, [CallerMemberName] string method = "")
    {
        var token = TracedToken.For(testClass, method);
        var raw = await RawSocketProbe.GetRawResponseAsync(
            Address, TracedToken.Append(pathAndQuery, token));
        return (raw, await StagesOrThrowAsync(token, pathAndQuery));
    }

    private async Task<string[]> StagesOrThrowAsync(string token, string pathAndQuery)
    {
        var stages = await Host.Witness.StagesAsync(token);
        return stages.Length > 0
            ? stages
            : throw new InvalidOperationException(
                "The witness recorded no stages for " + token + " on " + pathAndQuery
                + ": the request never entered the pipeline, or the fixture does not register"
                + " the stage-recording module.");
    }
}

public abstract class ScopedWitnessScenario : WitnessScenario
{
    private protected ScopedWitnessScenario(LiveScenario host)
        : base(host)
    {
    }
}

public abstract class WholeWitnessScenario : WitnessScenario
{
    private protected WholeWitnessScenario(LiveScenario host)
        : base(host)
    {
    }

    internal HostWitness Witness => Host.Witness;
}

// Class + method makes tokens collision-free between classes interleaving on a shared host; a
// bare literal reused by two classes would silently pool their stages. Method name alone is not
// enough: classes legitimately repeat method names.
file static class TracedToken
{
    internal static string For(object testClass, string method)
    {
        if (testClass is LiveScenario or Scenario)
        {
            throw new ArgumentException(
                "Pass the test class, not the scenario: the token must carry the class whose"
                + " stages it keys.",
                nameof(testClass));
        }

        return testClass.GetType().Name + "." + method;
    }

    internal static string Append(string pathAndQuery, string token) =>
        pathAndQuery
        + (pathAndQuery.Contains('?') ? "&" : "?")
        + WitnessProtocol.TokenKey + "=" + token;
}

public sealed class PageLiveScenario(ScenarioHostRegistry registry)
    : ScopedWitnessScenario(registry.GetOrAdd(Fixtures.Page));

public sealed class PostbackLiveScenario(ScenarioHostRegistry registry)
    : Scenario(registry.GetOrAdd(Fixtures.Postback));

// Probes that neither change a host-level limit nor abort their connection share this host.
public sealed class BodyLiveScenario(ScenarioHostRegistry registry)
    : Scenario(registry.GetOrAdd(Fixtures.Body));

// Both aborts kill their socket mid-read, so they never share a connection with anything and can
// share a host with each other, but stay off the main body host — hence the registry role.
public sealed class AbortLiveScenario(ScenarioHostRegistry registry)
    : WholeWitnessScenario(registry.GetOrAdd(Fixtures.Body, role: "abort"));

public sealed class SessionLiveScenario(ScenarioHostRegistry registry)
    : WholeWitnessScenario(registry.GetOrAdd(Fixtures.Session));

public sealed class SessionCustomLiveScenario(ScenarioHostRegistry registry)
    : WholeWitnessScenario(registry.GetOrAdd(Fixtures.SessionCustom));

public sealed class AsyncAppLiveScenario(ScenarioHostRegistry registry)
    : Scenario(registry.GetOrAdd(Fixtures.AsyncApp));

public sealed class FriendlyUrlsLiveScenario(ScenarioHostRegistry registry)
    : Scenario(registry.GetOrAdd(Fixtures.FriendlyUrls));

public sealed class AuthLiveScenario(ScenarioHostRegistry registry)
    : Scenario(registry.GetOrAdd(Fixtures.Auth));

public sealed class DefDocDisabledLiveScenario(ScenarioHostRegistry registry)
    : Scenario(registry.GetOrAdd(Fixtures.DefDocDisabled));

public sealed class CustomErrorsLiveScenario(ScenarioHostRegistry registry)
    : Scenario(registry.GetOrAdd(Fixtures.BodyCustomErrors));

public sealed class WebServerLiveScenario(ScenarioHostRegistry registry)
    : Scenario(registry.GetOrAdd(Fixtures.WebServer));

public sealed class ModulesLiveScenario(ScenarioHostRegistry registry)
    : ScopedWitnessScenario(registry.GetOrAdd(Fixtures.Modules));

public sealed class ModulesRunAllLiveScenario(ScenarioHostRegistry registry)
    : ScopedWitnessScenario(registry.GetOrAdd(Fixtures.ModulesRunAll));

public sealed class HandlersLiveScenario(ScenarioHostRegistry registry)
    : Scenario(registry.GetOrAdd(Fixtures.Handlers));

public sealed class MigratedLiveScenario(ScenarioHostRegistry registry)
    : ScopedWitnessScenario(registry.GetOrAdd(Fixtures.Migrated));

// The timeout probes hang and expire their requests, so the host is damaged goods: one private
// process per consuming class, never the registry.
public sealed class TimeoutLiveScenario()
    : WholeWitnessScenario(
        LiveScenario.StartIsolated(Fixtures.Timeout, IsolationReason.ProcessDamage)),
    IDisposable
{
    public void Dispose() => Host.Dispose();
}

// Dedicated host over the page payload: the sweep probe presents every registered request as
// expired, so any other class's in-flight request on the same host would be spuriously timed
// out. Never route this through the shared registry.
public sealed class SweepLiveScenario()
    : WholeWitnessScenario(
        LiveScenario.StartIsolated(Fixtures.Page, IsolationReason.ProcessDamage)),
    IDisposable
{
    public void Dispose() => Host.Dispose();
}
