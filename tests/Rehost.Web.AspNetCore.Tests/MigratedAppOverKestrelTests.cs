using Rehost.Web.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// The surveyed production shape end to end: webServer-only modules, the Session swap, handler
// remove/re-add against an inherited name, and both classic sections present as dead text under
// validateIntegratedModeConfiguration="false".
public sealed class MigratedAppOverKestrelTests(MigratedLiveScenario scenario)
    : IClassFixture<MigratedLiveScenario>
{
    // Transcribed from the golden (MH1), with the app's two fresh adds appended and the swapped
    // name still standing where it was inherited.
    private static readonly string[] EffectiveModules =
    {
        "OutputCache",
        "Session",
        "WindowsAuthentication",
        "FormsAuthentication",
        "DefaultAuthentication",
        "RoleManager",
        "UrlAuthorization",
        "FileAuthorization",
        "AnonymousIdentification",
        "Profile",
        "UrlMappingsModule",
        "UrlRoutingModule-4.0",
        "ScriptModule-4.0",
        "Gatekeeper",
        "ResponseHeaders",
    };

    [Fact]
    public async Task The_Effective_Module_List_Keeps_The_Swapped_Name_At_Its_Inherited_Position()
    {
        var response = await scenario.Client.GetAsync(ProbePaths.ModuleList);

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldBe("MODULES:" + string.Join("|", EffectiveModules));
    }

    // MH2: the replacement runs where "Session" was inherited, so its stages open every event
    // group ahead of the two modules the application appended. An implementation that appended
    // the re-added name instead would put session-swap last in each group.
    [Fact]
    public async Task The_Session_Replacement_Runs_In_The_Inherited_Position()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/Default.aspx");

        response.StatusCode.ShouldBe(200, response.Text);
        stages.ShouldBe(
        [
            "session-swap:BeginRequest",
            "gatekeeper:BeginRequest",
            "header:BeginRequest",
            "session-swap:AuthenticateRequest",
            "gatekeeper:AuthenticateRequest",
            "header:AuthenticateRequest",
            // Within one module and one event, Framework builds the async steps ahead of the
            // sync ones (CreateEventExecutionSteps), whatever the registration order.
            "session-swap:AcquireRequestStateAsync",
            "session-swap:AcquireRequestState",
            "gatekeeper:AcquireRequestState",
            "header:AcquireRequestState",
            "session-swap:PostRequestHandlerExecute",
            "gatekeeper:PostRequestHandlerExecute",
            "header:PostRequestHandlerExecute",
            "session-swap:EndRequest",
            "gatekeeper:EndRequest",
            "header:EndRequest",
        ]);
    }

    // MH10: the swap carries the managedHandler condition its Framework original did, so a static
    // file sees only the two unconditioned application modules.
    [Fact]
    public async Task A_Static_File_Runs_The_Unconditioned_Application_Modules()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/asset.txt");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldContain("STATIC-OK", Case.Sensitive);
        response.Headers["X-Migrated"].ShouldBe("header-module");
        stages.ShouldBe(
        [
            "gatekeeper:BeginRequest",
            "header:BeginRequest",
            "gatekeeper:AuthenticateRequest",
            "header:AuthenticateRequest",
            "gatekeeper:AcquireRequestState",
            "header:AcquireRequestState",
            "gatekeeper:PostRequestHandlerExecute",
            "header:PostRequestHandlerExecute",
            "gatekeeper:EndRequest",
            "header:EndRequest",
        ]);
    }

    // A webServer-only auth module gates a natively-served asset, which is the reason the
    // condition is absent from the entry.
    [Fact]
    public async Task The_WebServer_Only_Auth_Module_Gates_A_Static_Asset()
    {
        var denied = await scenario.Client.GetAsync("/private/secret.txt");
        var allowed = await scenario.Client.GetWithHeadersAsync(
            "/private/secret.txt", ("X-Pass", "yes"));

        denied.StatusCode.ShouldBe(403);
        denied.Text.ShouldBe("DENIED-BY:gatekeeper");
        allowed.StatusCode.ShouldBe(200, allowed.Text);
        allowed.Text.ShouldContain("PRIVATE-OK", Case.Sensitive);
    }

    // MH9v: the re-added inherited name is active for the URLs nothing else claims, and still
    // matches behind the application's own fresh adds even though it is listed first.
    [Fact]
    public async Task A_Fresh_Mapping_Outranks_The_Re_Added_Inherited_Name()
    {
        var fresh = await scenario.Client.GetAsync("/report");
        var unclaimed = await scenario.Client.GetAsync("/nothing-here");

        fresh.Text.ShouldBe("HANDLED-BY:A");
        unclaimed.Text.ShouldBe("HANDLED-BY:B");
    }

    [Fact]
    public async Task An_Inherited_Mapping_The_Application_Left_Alone_Still_Serves_Its_Page()
    {
        var response = await scenario.Client.GetAsync("/Default.aspx");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldContain("migrated-fixture");
    }

    // MH7 and MH4: the classic copy of a dual-registered name never runs, and the classic-only
    // name is absent from the pipeline entirely.
    [Fact]
    public async Task The_Classic_Module_Section_Is_Dead_Text()
    {
        var (_, stages) = await scenario.TracedGetAsync(this, "/Default.aspx");

        stages.Count(stage => stage == "gatekeeper:BeginRequest").ShouldBe(1);
        stages.ShouldNotContain("classic-copy:BeginRequest");
        stages.ShouldNotContain("classic-only:BeginRequest");
    }

    // MH16: the classic mapping is never consulted, so the URL falls through the merged list to
    // the catch-all and 404s on a file that is not there.
    [Fact]
    public async Task The_Classic_Handler_Section_Is_Dead_Text()
    {
        var response = await scenario.Client.GetAsync("/probe3.axd");

        response.StatusCode.ShouldBe(404);
        response.Text.ShouldNotContain("HANDLED-BY");
    }
}
