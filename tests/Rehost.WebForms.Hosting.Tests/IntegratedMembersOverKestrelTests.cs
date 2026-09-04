using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Three members an integrated pool answered and a classic one refused outright: the
// WEBSOCKET_VERSION server variable (IV23), Response.SubStatusCode (IV26) and
// Request.InsertEntityBody (IV28).
public sealed class IntegratedMembersOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    // The count is the point: the name answers from the server-variable shortcut and must not
    // join the enumerated collection, which IV23 measured at 45 on both pools.
    [Fact]
    public async Task The_WebSocket_Version_Answers_Without_Joining_The_Collection()
    {
        var lines = await Lines("mode=vars");

        lines["websocket-version"].ShouldBe("13");
        lines["count"].ShouldBe("45");
        lines["in-allkeys"].ShouldBe("false");
        lines["unknown"].ShouldBe("null");
    }

    [Fact]
    public async Task The_Substatus_Defaults_To_Zero_And_Round_Trips()
    {
        var lines = await Lines("mode=substatus&sub=7");

        lines["default"].ShouldBe("0");
        lines["after"].ShouldBe("7");
    }

    // Setting StatusCode resets the substatus, so the pair has to be set in that order.
    [Fact]
    public async Task A_Substatus_Set_After_A_Status_Survives_It()
    {
        var lines = await Lines("mode=substatus&status=404&sub=8", expected: 404);

        lines["after"].ShouldBe("8");
    }

    [Fact]
    public async Task The_Substatus_Never_Reaches_The_Wire()
    {
        var plain = await scenario.Client.GetAsync(ProbePaths.Integrated + "?mode=substatus-wire");
        var stamped = await scenario.Client.GetAsync(
            ProbePaths.Integrated + "?mode=substatus-wire&sub=7");

        stamped.StatusCode.ShouldBe(plain.StatusCode);
        stamped.ReasonPhrase.ShouldBe(plain.ReasonPhrase);
        stamped.Text.ShouldBe(plain.Text);
        Comparable(stamped).ShouldBe(Comparable(plain));
    }

    [Fact]
    public async Task Insert_Entity_Body_Returns_And_Keeps_Its_Argument_Checks()
    {
        var lines = await Lines("mode=ieb", body: "field=inserted");

        lines["form"].ShouldBe("inserted");
        lines["no-args"].ShouldBe("ok");
        lines["three-args"].ShouldBe("ok");
        lines["null-buffer"].ShouldBe("ArgumentNullException");
        lines["negative-offset"].ShouldBe("ArgumentOutOfRangeException");
        lines["bad-range"].ShouldBe("ArgumentException");
        lines["form-after"].ShouldBe("inserted");
    }

    private static string Comparable(ScenarioResponse response) =>
        string.Join(
            "\n",
            response.Headers
                .Where(header => !string.Equals(header.Key, "Date", StringComparison.OrdinalIgnoreCase))
                .OrderBy(header => header.Key, StringComparer.Ordinal)
                .Select(header => header.Key + ": " + header.Value));

    private async Task<Dictionary<string, string>> Lines(
        string query, string? body = null, int expected = 200)
    {
        var path = ProbePaths.Integrated + "?" + query;
        var response = body == null
            ? await scenario.Client.GetAsync(path)
            : await scenario.Client.PostFormAsync(path, body);

        response.StatusCode.ShouldBe(expected, response.Text);
        return response.Text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
    }
}
