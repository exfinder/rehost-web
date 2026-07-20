using Shouldly;
using System.Web.Compilation;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

public sealed class XsdBuildProviderTests
{
    [Fact]
    public void Typed_DataSet_generation_is_explicitly_unsupported()
    {
        var provider = new XsdBuildProvider();

        var exception = Should.Throw<PlatformNotSupportedException>(() => provider.GenerateCode(null!));

        exception.Message.ShouldBe(
            "App_Code typed DataSet generation from XSD is not supported by Rehost.WebForms.");
    }
}
