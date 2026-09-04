using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Response.ClientDisconnectedToken and Request.Abort, refused on a classic pool and answered on
// an integrated one (IV24, IV25). Both live on the abort host: their clients kill their sockets.
public sealed class ClientDisconnectOverKestrelTests(AbortLiveScenario scenario)
    : IClassFixture<AbortLiveScenario>
{
    private static readonly TimeSpan WitnessBudget = TimeSpan.FromSeconds(60);

    // IV24: the token is cancelable for the whole request and still readable from
    // AddOnRequestCompleted, with the same values.
    [Fact]
    public async Task The_Token_Is_Cancelable_In_The_Handler_And_After_The_Request()
    {
        var response = await scenario.Client.GetAsync(ProbePaths.Disconnect + "?mode=token");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("token");
        (await scenario.Witness.WaitForAsync(WitnessProtocol.DisconnectToken, WitnessBudget))
            .ShouldBe(WitnessProtocol.DisconnectToken + "True/False");
        (await scenario.Witness.WaitForAsync(WitnessProtocol.DisconnectCompleted, WitnessBudget))
            .ShouldBe(WitnessProtocol.DisconnectCompleted + "True/False");
    }

    // The port is eager where IIS waited for the server's next write (IV24): closing the socket
    // is enough, with no further write from the handler.
    [Fact]
    public async Task Closing_The_Socket_Cancels_The_Token_Without_A_Further_Write()
    {
        await using (var reader = await RawSocketProbe.OpenAsync(
            scenario.Address, ProbePaths.Disconnect + "?mode=hold"))
        {
            (await reader.ReadUntilAsync("HOLDING")).ShouldContain("HOLDING");
        }

        (await scenario.Witness.WaitForAsync(WitnessProtocol.DisconnectHeld, WitnessBudget))
            .ShouldBe(WitnessProtocol.DisconnectHeld + "True");
    }

    // IV25: Abort returns, the code after it runs, and the client's connection is reset.
    [Fact]
    public async Task Abort_Resets_The_Connection_And_Lets_The_Handler_Run_On()
    {
        await using (var reader = await RawSocketProbe.OpenAsync(
            scenario.Address, ProbePaths.Disconnect + "?mode=abort"))
        {
            (await reader.ReadUntilAsync("BEFORE-ABORT")).ShouldContain("BEFORE-ABORT");
            (await reader.ReadToResetAsync()).ShouldBeTrue();
        }

        (await scenario.Witness.WaitForAsync(WitnessProtocol.AbortReturned, WitnessBudget))
            .ShouldBe(WitnessProtocol.AbortReturned + "returned");
        (await scenario.Witness.WaitForAsync(WitnessProtocol.AbortConnected, WitnessBudget))
            .ShouldBe(WitnessProtocol.AbortConnected + "False");

        // The boundary IV25 leaves open here: integrated raised HttpException from this flush,
        // where Kestrel discards a write to an aborted connection without an error.
        (await scenario.Witness.WaitForAsync(WitnessProtocol.AbortFlushed, WitnessBudget))
            .ShouldBe(WitnessProtocol.AbortFlushed + "ok");
    }
}
