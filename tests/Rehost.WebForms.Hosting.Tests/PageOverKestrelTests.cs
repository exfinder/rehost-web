using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

public sealed class PageScenario : IDisposable
{
    internal const string PageRequest = "/Default.aspx?value=a%26c%20%22q%22%20%C3%A9";

    internal ScenarioRun Run { get; } = ScenarioRun.Serve(PageRequest, "/Default.aspx.cs");

    public void Dispose()
    {
        Run.Dispose();
    }
}

// Activating an application permanently mutates process-global state, so the application is served
// from a child process. This is the only coverage of a dynamically compiled page crossing the
// adapter and a real socket; every other Kestrel assertion here runs precompiled handlers whose
// responses are far smaller than a rendered page.
//
// Both requests share that process. The fixture is written for it: Default.aspx.cs never appends
// to PageProbe.Stages, so a second request renders the same body as the first.
public sealed class PageOverKestrelTests(PageScenario scenario) : IClassFixture<PageScenario>
{
    private ScenarioRun Run => scenario.Run;

    [Fact]
    public void Serves_A_Dynamically_Compiled_Page_Over_Kestrel()
    {
        Run.Trace.ShouldContain("request:" + PageScenario.PageRequest + ":200");
        Run.Trace.ShouldContain("content-type:text/html; charset=utf-8");
        Run.Response(PageScenario.PageRequest).ShouldBe(Run.ExpectedResponse);
    }

    [Fact]
    public void Refuses_A_Path_Framework_Maps_To_The_Forbidden_Handler()
    {
        // The page is routed by the shipped root configuration, so this also covers the *.aspx
        // httpHandlers mapping reaching PageHandlerFactory.
        Run.Trace.ShouldContain("request:/Default.aspx.cs:403");
    }
}
