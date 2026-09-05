using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

public sealed class OwinCookieAuthenticationOverKestrelTests(FriendlyUrlsLiveScenario scenario)
    : IClassFixture<FriendlyUrlsLiveScenario>
{
    private const string CookieName = ".AspNet.ApplicationCookie";

    // Katana's redirect carries an absolute URI, unlike the relative Location the
    // FriendlyUrls resolver writes, and ReturnUrl echoes the requested path verbatim.
    private string LoginRedirect(string returnUrl) =>
        new Uri(scenario.Address, "/Login?ReturnUrl=" + Uri.EscapeDataString(returnUrl)).AbsoluteUri;

    [Fact]
    public async Task AnonymousRequestForAProtectedPageRedirectsToTheOwinLoginPath()
    {
        var direct = await scenario.Client.GetAsync("/Protected.aspx");
        var friendly = await scenario.Client.GetAsync("/Protected");

        direct.StatusCode.ShouldBe(302, direct.Text);
        direct.Headers["Location"].ShouldBe(LoginRedirect("/Protected.aspx"));
        friendly.StatusCode.ShouldBe(302, friendly.Text);
        friendly.Headers["Location"].ShouldBe(LoginRedirect("/Protected"));
    }

    [Fact]
    public async Task SignInIssuesTheApplicationCookieAndTheProtectedPageThenServes()
    {
        var signin = await scenario.Client.GetAsync("/Login?signin=1");

        signin.StatusCode.ShouldBe(200, signin.Text);
        signin.Text.ShouldBe("signed-in");
        var cookie = signin.SetCookies.Single(header => header.StartsWith(CookieName + "="));

        var protectedPage = await scenario.Client.GetWithCookiesAsync(
            "/Protected",
            cookie.Split(';')[0]);

        protectedPage.StatusCode.ShouldBe(200, protectedPage.Text);
        protectedPage.Text.ShouldBe("secret");
    }

    [Fact]
    public async Task SignOutExpiresTheCookieAndTheProtectedPageRedirectsAgain()
    {
        var signout = await scenario.Client.GetAsync("/Login?signout=1");

        signout.StatusCode.ShouldBe(200, signout.Text);
        signout.Text.ShouldBe("signed-out");
        var cleared = signout.SetCookies.Single(header => header.StartsWith(CookieName + "="));
        cleared.ShouldContain(CookieName + "=;");
        cleared.ShouldContain("expires=Thu, 01-Jan-1970 00:00:00 GMT", Case.Sensitive);

        var withCleared = await scenario.Client.GetWithCookiesAsync(
            "/Protected",
            cleared.Split(';')[0]);

        withCleared.StatusCode.ShouldBe(302, withCleared.Text);
        withCleared.Headers["Location"].ShouldBe(LoginRedirect("/Protected"));
    }
}
