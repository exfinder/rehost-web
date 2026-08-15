using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// ListView lives in System.Web.Extensions, and its ItemType drives two leaves the port had
// to answer: the asp: registration of the Extensions WebControls namespace, and
// BuildManager.GetType over a bare BCL name, which needs mscorlib in <assemblies>. Every
// data control declaring ItemType also reaches System.Web.DynamicData (ledger P69).
public sealed class ModelBoundListViewOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task ListView_Binds_Its_ItemType_Select_Method()
    {
        var response = await scenario.Client.GetAsync("/ModelBoundListView.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("<span class=\"name\">alpha</span>");
        response.Text.ShouldContain("<span class=\"name\">beta</span>");
        response.Text.ShouldNotContain("no names");
    }
}
