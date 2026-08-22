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

public abstract class ScopedWitnessScenario : Scenario
{
    private protected ScopedWitnessScenario(LiveScenario host)
        : base(host)
    {
    }

    internal ScopedWitness Witness => new(Host.Witness);
}

public abstract class WholeWitnessScenario : Scenario
{
    private protected WholeWitnessScenario(LiveScenario host)
        : base(host)
    {
    }

    internal HostWitness Witness => Host.Witness;
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

public sealed class WebServerLiveScenario(ScenarioHostRegistry registry)
    : Scenario(registry.GetOrAdd(Fixtures.WebServer));

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
