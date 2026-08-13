using Shouldly;
using Rehost.WebForms.Parity.Contracts;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

public sealed class CodegenCompileErrorTests
{
    [Fact]
    public void Reports_A_Compile_Error_On_Every_Request_Without_Failing_Activation()
    {
        using var application = ScenarioApplication.Create();
        application.BreakAppCode();

        // Framework stashes an initialization failure and renders it per request rather than
        // aborting activation, which is what CreateObject's throwOnError:false selects. Aborting
        // instead would take the diagnostics away from whoever asked for the page.
        var trace = application.Run("/default", "/default");

        trace.FindAll(entry => entry == "request:/default:500").Count.ShouldBe(2);
        var diagnostics = trace.FindAll(entry => entry.StartsWith(TraceEvents.ErrorBody));
        diagnostics.Count.ShouldBe(2);
        diagnostics[0].ShouldContain("Compilation Error");
        diagnostics[1].ShouldBe(diagnostics[0]);
    }
}
