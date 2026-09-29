using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// Six imported controls carry [ToolboxBitmap]. The System.Drawing.Common attribute of that name
// reaches GDI+ from its type initializer, which the parser runs when it materializes control
// attributes, so declaring any of them failed at parse time off Windows (ledger P66).
public sealed class DesignTimeMetadataOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task Controls_Carrying_Toolbox_Metadata_Parse_And_Render()
    {
        var response = await scenario.Client.GetAsync("/DesignTimeControls.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("localize-rendered");
    }
}
