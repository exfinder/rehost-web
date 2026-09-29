using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// The five events the IIS modules rig recorded, and the interleaving it measured: same-event
// firing follows effective list order and the end-side events are not reversed (MH1).
file static class ModuleStages
{
    private static readonly string[] Names =
    {
        "BeginRequest",
        "AuthenticateRequest",
        "AcquireRequestState",
        "PostRequestHandlerExecute",
        "EndRequest",
    };

    internal static string[] Interleaved(params string[] modules) =>
        [.. Names.SelectMany(stage => modules.Select(module => module + ":" + stage))];
}

// The pipeline's module collection comes from the merged system.webServer/modules snapshot. Full
// stage sequences, not membership: a module that ran twice, ran out of list order, or ran a
// request it should have skipped fails here.
public sealed class ModulesOverKestrelTests(ModulesLiveScenario scenario)
    : IClassFixture<ModulesLiveScenario>
{
    [Fact]
    public async Task A_Managed_Request_Runs_Every_Registered_Module_In_List_Order()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/Default.aspx");

        response.StatusCode.ShouldBe(200);
        stages.ShouldBe(ModuleStages.Interleaved(
            "unconditioned", "managed-handler", "webserver-copy", "dynamic", "dynamic-utility"));
    }

    // A rewrite moves the handler, not the managedHandler answer: the page serves the request
    // and the conditioned module stays skipped for all five events.
    [Fact]
    public async Task A_Static_Url_Rewritten_Onto_A_Page_Keeps_The_Conditioned_Module_Skipped()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/rewrite-to-page.txt");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("modules-fixture");
        stages.ShouldBe(ModuleStages.Interleaved("unconditioned", "webserver-copy"));
    }

    [Fact]
    public async Task A_Static_Url_Remapped_To_A_Handler_Keeps_The_Conditioned_Module_Skipped()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/remap-to-handler.txt");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("REMAPPED");
        stages.ShouldBe(ModuleStages.Interleaved("unconditioned", "webserver-copy"));
    }

    // The other direction: a native handler serves the bytes and the conditioned module still
    // runs throughout.
    [Fact]
    public async Task A_Page_Url_Rewritten_Onto_A_Static_File_Keeps_The_Conditioned_Module_Running()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/rewrite-to-asset.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.Trim().ShouldBe("STATIC-OK");
        stages.ShouldBe(ModuleStages.Interleaved(
            "unconditioned", "managed-handler", "webserver-copy", "dynamic", "dynamic-utility"));
    }

    // MH10: the unconditioned module sees the static file and the managedHandler one does not. A
    // dynamic registration carries the implicit condition, so it is skipped here too.
    [Fact]
    public async Task A_Static_File_Runs_Only_The_Unconditioned_Modules()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/asset.txt");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("STATIC-OK", Case.Sensitive);
        stages.ShouldBe(ModuleStages.Interleaved("unconditioned", "webserver-copy"));
    }

    // MH10 through the folder list (MH27): the extension the root serves natively is mapped to a
    // managed handler inside this folder, and the conditioned module runs there. The condition is
    // decided against the request's own directory, not the application root's list.
    [Fact]
    public async Task A_Folder_Mapping_Decides_The_Managed_Handler_Condition()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/managed/asset.txt");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("HANDLED-BY:A");
        stages.ShouldBe(ModuleStages.Interleaved(
            "unconditioned", "managed-handler", "webserver-copy", "dynamic", "dynamic-utility"));
    }

    // MH3 and MH7: the name registered in both sections runs once, from the webServer type, and a
    // classic-only registration never runs at all (MH4).
    [Fact]
    public async Task Classic_Registrations_Are_Dead_Text()
    {
        var (_, stages) = await scenario.TracedGetAsync(this, "/Default.aspx");

        stages.Count(stage => stage == "webserver-copy:BeginRequest").ShouldBe(1);
        stages.ShouldNotContain("classic-copy:BeginRequest");
        stages.ShouldNotContain("classic-only:BeginRequest");
    }
}

// MH17: the flag clears the managedHandler condition for the whole collection, so the module the
// modules fixture skips on a static file runs on one here.
public sealed class ModulesRunAllOverKestrelTests(ModulesRunAllLiveScenario scenario)
    : IClassFixture<ModulesRunAllLiveScenario>
{
    [Fact]
    public async Task RunAllManagedModulesForAllRequests_Runs_Conditioned_Modules_On_A_Static_File()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/asset.txt");

        response.StatusCode.ShouldBe(200);
        stages.ShouldBe(ModuleStages.Interleaved("managed-handler", "dynamic", "dynamic-utility"));
    }
}
