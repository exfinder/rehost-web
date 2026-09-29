using Shouldly;
using Rehost.Web.TestSupport;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.Compilation;

// The first run is the shared page fixture's; only the restart run is paid for here.
[Collection(nameof(PageCompilationCollection))]
public sealed class PageReuseTests(PageCompilationFixture fixture)
{
    private const string Request = PageRequests.Canonical;

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
