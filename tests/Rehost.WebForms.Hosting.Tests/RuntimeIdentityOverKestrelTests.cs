using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Reading IV16 in a running application: an integrated IIS 10 pool reports True and 10.0,
// the classic pool on the same server False and 8.0 (ADR 0013).
public sealed class RuntimeIdentityOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task TheApplicationReadsTheIdentityOfAnIntegratedIis10Pool()
    {
        var response = await scenario.Client.GetAsync(ProbePaths.RuntimeIdentity);

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldBe("integrated=True;iis=10.0");
    }
}
