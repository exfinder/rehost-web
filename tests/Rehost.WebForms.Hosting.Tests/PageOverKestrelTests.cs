using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Activating an application permanently mutates process-global state, so the application is served
// from a child process. This is the only coverage of a dynamically compiled page crossing the
// adapter and a real socket; every other Kestrel assertion here runs precompiled handlers whose
// responses are far smaller than a rendered page.
public sealed class PageOverKestrelTests
{
    private const string Request = "/Default.aspx?value=a%26c%20%22q%22%20%C3%A9";

    [Fact]
    public void Serves_A_Dynamically_Compiled_Page_Over_Kestrel()
    {
        using var run = ScenarioRun.Serve(Request);

        run.Trace.ShouldContain("request:" + Request + ":200");
        run.Trace.ShouldContain("content-type:text/html; charset=utf-8");
        run.Response(0).ShouldBe(run.ExpectedResponse);
    }

    [Fact]
    public void Refuses_A_Path_Framework_Maps_To_The_Forbidden_Handler()
    {
        using var run = ScenarioRun.Serve("/Default.aspx.cs");

        // The page is routed by the shipped root configuration, so this also covers the *.aspx
        // httpHandlers mapping reaching PageHandlerFactory.
        run.Trace.ShouldContain("request:/Default.aspx.cs:403");
    }
}
