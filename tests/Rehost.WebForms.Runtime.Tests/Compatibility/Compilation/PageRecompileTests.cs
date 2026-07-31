using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

public sealed class PageRecompileTests
{
    private const string Request = "/Default.aspx?value=a%26c%20%22q%22%20%C3%A9";

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
