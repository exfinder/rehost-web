using Rehost.Web.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

public sealed class DynamicValidationOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task The_Unvalidated_Getters_Read_What_Request_Form_And_QueryString_Refuse()
    {
        var response = await scenario.Client.PostFormAsync(
            $"{ProbePaths.DynamicValidation}?q=%3Cscript%3E", "x=%3Cscript%3E&safe=plain");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe(
            """
            enabled-before=True
            enabled-after=True
            form-getter=<script>
            query-getter=<script>
            form-getter-same=True
            form-getter-unvalidated=True
            form=HttpRequestValidationException
            query=HttpRequestValidationException
            safe=plain
            """.ReplaceLineEndings("\n"));
    }
}
