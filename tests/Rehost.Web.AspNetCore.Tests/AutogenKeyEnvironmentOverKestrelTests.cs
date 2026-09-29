using Rehost.Web.Hosting;
using Rehost.Web.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// Environment-supplied machine keys must behave exactly like the same literal keys in web.config
// (ADR 0010). The proof is a ticket interchange: a ticket minted under environment keys validates
// on the auth fixture's shared host, whose web.config pins the identical key values.
public sealed class AutogenKeyEnvironmentOverKestrelTests(AuthLiveScenario configKeyed)
    : IClassFixture<AuthLiveScenario>
{
    private const string AuthCookie = ".ASPXAUTH";

    // fixtures/auth/web.config's literal keys, spelled here because the interchange claim is
    // exactly that these bytes and the environment bytes derive the same ticket.
    private const string ValidationKey =
        "E5E96E17E2FC4E4F325AD4B9374112BC5298EA7F6833E5EB2829FA61DEA8ABC7DBF63EDD75EE4A5B5B8E14A71B0F9E375BB35059AD8F42F314B48F1AB9AD337B";
    private const string DecryptionKey =
        "5D96763F754B1A37C60A65B3B47C54684BDFE91CB5F56870021D5DA0477F6C3A";

    [Fact]
    public async Task A_Ticket_Minted_Under_Environment_Keys_Validates_Against_Config_Keys()
    {
        using var environmentKeyed = LiveScenario.StartIsolated(
            Fixtures.Autogen,
            IsolationReason.HostConfiguration,
            environment: new Dictionary<string, string>
            {
                [RehostWebOptions.MachineKeyValidationKeyVariable] = ValidationKey,
                [RehostWebOptions.MachineKeyDecryptionKeyVariable] = DecryptionKey,
            });

        var signIn = await environmentKeyed.Client.GetAsync(ProbePaths.AuthSignIn + "?u=alice&p=pw");
        signIn.Text.ShouldContain("validate:True", Case.Sensitive);
        var ticket = signIn.SetCookies
            .First(line => line.StartsWith(AuthCookie + "=", StringComparison.Ordinal))
            .Split(';')[0];

        var protectedPage = await configKeyed.Client.GetWithCookiesAsync(
            ProbePaths.Secret, ticket);

        protectedPage.StatusCode.ShouldBe(200);
        protectedPage.Text.ShouldContain("secret-ok:alice");
        Directory.Exists(environmentKeyed.MachineKeyDirectory).ShouldBeFalse();
    }
}
