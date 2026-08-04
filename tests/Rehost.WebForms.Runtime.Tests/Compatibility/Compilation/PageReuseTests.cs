using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

// The first run is the shared page fixture's; only the restart run is paid for here.
[Collection(nameof(PageCompilationCollection))]
public sealed class PageReuseTests(PageCompilationFixture fixture)
{
    private const string Request = "/Default.aspx?value=a%26c%20%22q%22%20%C3%A9";

    [Fact]
    public void Reuses_The_Page_Assembly_Across_A_Restart()
    {
        var application = fixture.Application;
        var first = application.PageAssemblies();

        application.Run(Request);

        first.ShouldNotBeEmpty();
        application.PageAssemblies().ShouldBe(first);
        application.ReadResponse(0).ShouldBe(application.ExpectedResponse);
    }
}
