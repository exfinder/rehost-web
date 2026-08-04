using Shouldly;
using System.Web.Compilation;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

public sealed class XsdBuildProviderTests
{
    [Fact]
    public void Typed_DataSet_Generation_Is_Explicitly_Unsupported()
    {
        var provider = new XsdBuildProvider();

        var exception = Should.Throw<PlatformNotSupportedException>(() => provider.GenerateCode(null!));

        exception.Message.ShouldContain("not supported");
    }
}
