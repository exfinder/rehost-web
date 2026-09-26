using Shouldly;
using Rehost.WebForms.ScenarioProtocol;
using Xunit;
using static Rehost.WebForms.Runtime.Tests.Compatibility.Compilation.BatchTrace;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

[Collection(nameof(CodegenSubstrateCollection))]
public sealed class CodegenSubstrateTests(CodegenSubstrateFixture fixture)
{
    [Fact]
    public void Compiles_The_Top_Level_Files_In_Order_Before_The_First_Request()
    {
        var first = fixture.FirstTrace;
        var applicationStart = IndexOf(first, "application-start:");

        // Pre-application start runs from a bin assembly before anything is generated; App_Code's
        // AppInitialize runs before Application_Start, which is the first thing that can observe a
        // compiled Global.asax.
        first.IndexOf("pre-start").ShouldBeLessThan(first.IndexOf("app-initialize"));
        first.IndexOf("app-initialize").ShouldBeLessThan(applicationStart);
        applicationStart.ShouldBeLessThan(first.IndexOf("begin-request"));
        first.IndexOf("begin-request").ShouldBeLessThan(first.IndexOf("handler"));
        first.ShouldContain("request:/default:200");
    }

    [Fact]
    public void Compiles_App_Code_Its_Subdirectory_And_Global_Resources()
    {
        var first = fixture.FirstTrace;

        // Global.asax used a type from App_Code, which used one from the sub-directory assembly
        // and the generated resource class. Separate load contexts would fail this, not the
        // assembly names.
        Value(first, TraceEvents.AppCode).ShouldStartWith("App_Code.", Case.Sensitive);
        Value(first, TraceEvents.SubCode).ShouldStartWith("App_SubCode_Shared.", Case.Sensitive);
        Value(first, TraceEvents.Resource).ShouldBe("neutral-greeting");

        fixture.AppCodeCount.ShouldBe(1);
        fixture.SubCodeCount.ShouldBe(1);
        fixture.GlobalResourcesCount.ShouldBe(1);
        fixture.SatelliteCount.ShouldBe(1);
    }

    [Fact]
    public void Assembly_Load_Resolves_The_Fixed_Names_Of_Generated_Assemblies()
    {
        var first = fixture.FirstTrace;
        var appCode = Value(first, TraceEvents.AppCode);

        Value(first, TraceEvents.FixedName + "App_Code=").ShouldBe(appCode);
        Value(first, TraceEvents.FixedName + "__code=").ShouldBe(appCode);
        Value(first, TraceEvents.FixedName + "App_SubCode_Shared=")
            .ShouldBe(Value(first, TraceEvents.SubCode));
        Value(first, TraceEvents.FixedName + "App_GlobalResources=")
            .ShouldStartWith("App_GlobalResources.", Case.Sensitive);
    }
}
