using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.Compilation;

public sealed class PageDeployedReferenceTests
{
    // The page reference set is seeded from the shared framework directory, which carries a lower
    // version of any assembly an out-of-band package advances. Framework built the set from the
    // assemblies the application had loaded, so the deployed copy has to win here too or the page
    // fails with CS1705 before it ever runs.
    [Fact]
    public void Compiles_A_Page_Against_The_Deployed_Assembly_Rather_Than_The_Shared_Framework()
    {
        using var application = PageApplication.Create();

        var trace = application.Run("/Formatter.aspx");

        trace.ShouldContain("request:/Formatter.aspx:200");
        // ObjectStateFormatter must have run, not merely resolved: 13 bytes is the token stream
        // for an ArrayList of one string and one int.
        application.ReadResponseText(0).ShouldContain("""<p id="length">13</p>""");
    }
}
