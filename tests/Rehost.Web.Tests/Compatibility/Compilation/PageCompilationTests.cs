using Shouldly;
using Rehost.Web.ScenarioProtocol;
using Rehost.Web.TestSupport;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.Compilation;

[Collection(nameof(PageCompilationCollection))]
public sealed class PageCompilationTests(PageCompilationFixture fixture)
{
    private const string Request = PageRequests.Canonical;

    [Fact]
    public void Compiles_And_Renders_A_Page_On_The_First_Request()
    {
        fixture.FirstTrace.ShouldContain(TraceEvents.Request + Request + ":200");
        fixture.FirstResponse.ShouldBe(fixture.ExpectedResponse);
    }

    [Fact]
    public void Renders_Values_Produced_By_App_Code_And_Global_Asax()
    {
        // These stages come from App_Code and Global.asax; separate load contexts would render
        // different text rather than fail.
        fixture.FirstResponseText
            .ShouldContain("""<p id="stages">app-initialize|application-start</p>""");
    }

    // Ledger P39. RoslynCSharpCodeProvider reports no GeneratorSupport.Win32Resources, so
    // UseResourceLiteralString is false and a literal run past the 256-character threshold stays
    // an ordinary metadata string. The Win32 read-back path is therefore unreachable rather than
    // ported, and StringResourceManager is inactive.
    [Fact]
    public void A_Compiled_Page_Reads_No_Literal_Through_A_Win32_String_Resource()
    {
        var evidence = fixture.AssemblyEvidence;

        evidence.Referenced.ShouldContain("Write");
        evidence.Referenced.ShouldNotContain("WriteUTF8ResourceString");
        evidence.Referenced.ShouldNotContain("CreateResourceBasedLiteralControl");
        evidence.Referenced.ShouldNotContain("SetStringResourcePointer");
        evidence.ResourceTableSize.ShouldBe(0);
        evidence.ContainsLongLiteral.ShouldBeTrue();
    }
}
