using System.Text.RegularExpressions;
using Shouldly;
using Rehost.WebForms.Parity.Contracts;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

public sealed class CodegenCompileErrorTests
{
    [Fact]
    public void Reports_A_Compile_Error_On_Every_Request_Without_Failing_Activation()
    {
        using var application = BatchApplication.Create();
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

    // The compiler names a duplicate at every declaration after the first it saw, and it sees
    // App_Code in directory order: NTFS order on Framework, sorted here on every filesystem. The
    // first file in that order is the one never blamed.
    [Fact]
    public void Blames_Duplicate_App_Code_Types_In_Directory_Order()
    {
        using var application = BatchApplication.Create();
        foreach (var name in new[] { "Zulu", "Golf", "bravo", "Foxtrot", "Delta", "Echo" })
        {
            application.AddAppCode(name + ".cs", "public class Twice { }");
        }

        var trace = application.Run("/default");

        trace.ShouldContain("request:/default:500");
        var blamed = Regex.Matches(application.ResponseBody(0), @"App_Code[\\/](\w+)\.cs\(")
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToList();
        blamed.ShouldBe(["Delta", "Echo", "Foxtrot", "Golf", "Zulu"]);
    }
}
