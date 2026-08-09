using Microsoft.AspNet.FriendlyUrls.Resolvers;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.FriendlyUrls.Tests.Resolvers;

public sealed class FriendlyUrlResolverTests
{
    [Fact]
    public void ConvertToFriendlyUrlStripsOnlyConfiguredExtension()
    {
        var resolver = new FriendlyUrlResolver(".aspx");

        resolver.ConvertToFriendlyUrl("/Catalog/Details.aspx")
            .ShouldBe("/Catalog/Details");
        resolver.ConvertToFriendlyUrl("/Catalog/Details.ashx").ShouldBeNull();
    }
}
