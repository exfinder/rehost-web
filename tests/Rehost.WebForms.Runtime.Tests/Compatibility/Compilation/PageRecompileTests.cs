using Shouldly;
using Rehost.WebForms.TestSupport;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

public sealed class PageRecompileTests
{
    private const string Request = PageRequests.Canonical;

    [Fact]
    public void Recompiles_The_Page_After_The_Markup_Changes()
    {
        using var application = PageApplication.Create();
        application.Run(Request);
        var first = application.PageAssemblies();

        application.EditMarkup();
        application.Run(Request);

        application.PageAssemblies().ShouldNotBe(first);
    }
}
