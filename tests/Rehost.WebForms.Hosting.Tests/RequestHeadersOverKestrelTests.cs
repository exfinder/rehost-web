using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// IV7-IV9: an application mutates its own request headers, Add appends comma-joined, Clear stays
// refused, and the two mirrors follow only under the laziness Framework itself imposes.
public sealed class RequestHeadersOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    private const string Agent = "probe-agent/1";
    private const string Referer = "https://referer.example/from";

    [Fact]
    public async Task Mutations_Are_Visible_On_A_Later_Read_And_On_The_Url()
    {
        var response = await Get("");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe(
            $"""
            clear=NotSupportedException
            probe=p
            multi=a,b
            agent-header=null
            agent-typed=probe-agent/1
            url-authority=rewritten.example:{scenario.Address.Port}

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task An_Already_Materialized_Server_Variable_Follows_The_Mutation()
    {
        var response = await Get("?mode=mirror");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe(
            """
            before-referer=https://referer.example/from
            clear=NotSupportedException
            sv-probe=p
            sv-referer=null

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Mirrors_Read_After_The_Mutation_Report_What_They_Cached()
    {
        var response = await Get("?mode=lazy");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe(
            $"""
            before-host={scenario.Address.Host}
            clear=NotSupportedException
            sv-referer=https://referer.example/from
            url-host={scenario.Address.Host}

            """.ReplaceLineEndings("\n"));
    }

    private Task<ScenarioResponse> Get(string query) =>
        scenario.Client.GetWithHeadersAsync(
            ProbePaths.RequestHeaders + query,
            ("User-Agent", Agent),
            ("Referer", Referer));
}
