using Microsoft.Web.Infrastructure;
using Shouldly;
using Xunit;

namespace Rehost.Web.Infrastructure.Tests;

public sealed class InfrastructureHelperTests
{
    [Theory]
    [InlineData(".cs", true)]
    [InlineData(".CS", true)]
    [InlineData(".vb", true)]
    [InlineData("cs", true)]
    [InlineData(".cpp", false)]
    [InlineData(".cshtml", false)]
    [InlineData(".aspx", false)]
    [InlineData("", false)]
    public void Code_Dom_Extensions_Answer_As_On_Framework(string extension, bool defined) =>
        InfrastructureHelper.IsCodeDomDefinedExtension(extension).ShouldBe(defined);

    [Theory]
    [InlineData(".js")]
    [InlineData(".h")]
    public void Framework_Only_JScript_And_Cpp_Extensions_Are_Not_Defined(string extension) =>
        InfrastructureHelper.IsCodeDomDefinedExtension(extension).ShouldBeFalse();

    [Fact]
    public void A_Null_Extension_Is_Refused() =>
        Should.Throw<ArgumentNullException>(
            () => InfrastructureHelper.IsCodeDomDefinedExtension(null!))
            .ParamName.ShouldBe("extension");
}
