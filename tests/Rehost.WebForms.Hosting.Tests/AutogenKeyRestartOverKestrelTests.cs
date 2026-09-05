using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The persisted autogen key claim (ADR 0010): a forms ticket issued under the shipped
// AutoGenerate machineKey default outlives the process that issued it, and the persisted key
// file is what carries it across.
public sealed class AutogenKeyRestartOverKestrelTests
{
    private const string AuthCookie = ".ASPXAUTH";

    [Fact]
    public async Task A_Forms_Ticket_Survives_A_Host_Restart()
    {
        using var scenario = LiveScenario.StartIsolated(
            Fixtures.Autogen, IsolationReason.ProcessDamage);
        var ticket = await SignIn(scenario);

        var beforeRestart = await scenario.Client.GetWithCookiesAsync(ProbePaths.Secret, ticket);
        scenario.KillAndRestart();
        var afterRestart = await scenario.Client.GetWithCookiesAsync(ProbePaths.Secret, ticket);

        beforeRestart.StatusCode.ShouldBe(200);
        afterRestart.StatusCode.ShouldBe(200);
        afterRestart.Text.ShouldContain("secret-ok:alice");
    }

    // Deleting the key file must invalidate the ticket; a run that passed the positive claim with
    // process-local keys would pass here too, so this is the control that pins the file as the
    // carrier.
    [Fact]
    public async Task Deleting_The_Key_File_Invalidates_The_Ticket_Across_Restart()
    {
        using var scenario = LiveScenario.StartIsolated(
            Fixtures.Autogen, IsolationReason.ProcessDamage);
        var ticket = await SignIn(scenario);

        Directory.Delete(scenario.MachineKeyDirectory, recursive: true);
        scenario.KillAndRestart();
        var afterRestart = await scenario.Client.GetWithCookiesAsync(ProbePaths.Secret, ticket);

        afterRestart.StatusCode.ShouldBe(302);
        afterRestart.Header("Location").ShouldStartWith("/Login.aspx", Case.Sensitive);
    }

    private static async Task<string> SignIn(LiveScenario scenario)
    {
        var signIn = await scenario.Client.GetAsync(ProbePaths.AuthSignIn + "?u=alice&p=pw");
        signIn.Text.ShouldContain("validate:True", Case.Sensitive);

        var ticket = signIn.SetCookies.FirstOrDefault(
            line => line.StartsWith(AuthCookie + "=", StringComparison.Ordinal));
        return ticket.ShouldNotBeNull().Split(';')[0];
    }
}
