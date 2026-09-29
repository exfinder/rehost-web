using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

public sealed class SqlDataSourceProviderOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task A_SqlDataSource_Resolves_A_Provider_Registered_In_Web_Config()
    {
        var response = await scenario.Client.GetAsync("/SqlDataSourceProvider.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("[alpha][beta]");
    }
}
