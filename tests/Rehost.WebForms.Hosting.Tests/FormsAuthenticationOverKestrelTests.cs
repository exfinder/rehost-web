using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Expected values come from readings R-FA1 to R-FA9 in docs/follow-ups/forms-authentication.md,
// taken on IIS Express over 4.8.1 against the same provider shapes.
public sealed class FormsAuthenticationOverKestrelTests(AuthLiveScenario scenario)
    : IClassFixture<AuthLiveScenario>
{
    private const string AuthCookie = ".ASPXAUTH";
    private const string RoleCookie = ".ASPXROLES";
    private const string AnonymousCookie = ".ASPXANONYMOUS";

    [Fact]
    public async Task A_Denied_Request_Redirects_To_The_Login_Page()
    {
        var response = await scenario.Client.GetAsync(ProbePaths.Secret);

        response.StatusCode.ShouldBe(302);
        response.Header("Location").ShouldBe("/Login.aspx?ReturnUrl=%2fSecret%2fsecret");
        Cookie(response, AnonymousCookie).ShouldNotBeNull();
    }

    [Fact]
    public async Task Signing_In_Authenticates_The_Next_Request()
    {
        var signIn = await scenario.Client.GetAsync(ProbePaths.AuthSignIn + "?u=alice&p=pw");
        signIn.Text.ShouldContain("validate:True");

        var ticket = Cookie(signIn, AuthCookie).ShouldNotBeNull();
        ticket.ShouldContain("path=/");
        ticket.ShouldContain("HttpOnly");
        ticket.ShouldNotContain("expires=");

        var report = Report(await scenario.Client.GetWithCookiesAsync(ProbePaths.AuthStatus, Value(ticket)));
        report["auth"].ShouldBe("True");
        report["name"].ShouldBe("alice");
        report["identity"].ShouldBe("FormsIdentity");

        var protectedPage = await scenario.Client.GetWithCookiesAsync(ProbePaths.Secret, Value(ticket));
        protectedPage.StatusCode.ShouldBe(200);
        protectedPage.Text.ShouldContain("secret-ok:alice");
    }

    [Fact]
    public async Task A_Role_Check_Writes_The_Role_Cookie_And_Answers_From_It()
    {
        var ticket = Value(Cookie(await scenario.Client.GetAsync(ProbePaths.AuthSignIn + "?u=rolereader&p=pw"), AuthCookie)!);

        var first = await scenario.Client.GetWithCookiesAsync(ProbePaths.AuthStatus, ticket);
        Report(first)["editors"].ShouldBe("True");
        Report(first)["principal"].ShouldBe("RolePrincipal");
        Report(first)["fetches"].ShouldBe("1");

        var roles = Cookie(first, RoleCookie).ShouldNotBeNull();
        roles.ShouldContain("path=/");
        roles.ShouldContain("HttpOnly");

        var second = await scenario.Client.GetWithCookiesAsync(ProbePaths.AuthStatus, ticket, Value(roles));
        Report(second)["editors"].ShouldBe("True");
        Report(second)["stage"].ShouldBe("RolePrincipal/True/cookie/cached=True/fetched=1");
        Report(second)["fetches"].ShouldBe("1");
    }

    [Fact]
    public async Task An_Anonymous_Visitor_Keeps_A_Profile_Value_Across_Requests()
    {
        var opening = await scenario.Client.GetAsync(ProbePaths.AuthStatus);
        Report(opening)["anonid"].ShouldBe("set");
        Report(opening)["profileuser"].ShouldBe("<anonymousid>");

        var anonymous = Value(Cookie(opening, AnonymousCookie).ShouldNotBeNull());
        (await scenario.Client.GetWithCookiesAsync(ProbePaths.AuthProfile + "?n=nick", anonymous))
            .Text.ShouldContain("saved:nick");

        var later = await scenario.Client.GetWithCookiesAsync(ProbePaths.AuthStatus, anonymous);
        Report(later)["nickname"].ShouldBe("[nick]");
    }

    [Fact]
    public async Task A_Page_That_Asks_To_Be_Cached_Is_Served_From_The_Store()
    {
        var anonymous = Value(Cookie(await scenario.Client.GetAsync(ProbePaths.AuthStatus), AnonymousCookie)!);
        var first = await scenario.Client.GetWithCookiesAsync("/Cached.aspx", anonymous);
        var second = await scenario.Client.GetWithCookiesAsync("/Cached.aspx", anonymous);

        first.StatusCode.ShouldBe(200);
        first.Text.ShouldStartWith("stamp:");
        second.Text.ShouldBe(first.Text);
        first.Header("Cache-Control").ShouldBe("public, max-age=30");
        second.Header("Expires").ShouldBe(first.Header("Expires"));
    }

    private static string? Cookie(ScenarioResponse response, string name) =>
        response.SetCookies.FirstOrDefault(line => line.StartsWith(name + "=", StringComparison.Ordinal));

    private static string Value(string setCookie) => setCookie.Split(';')[0];

    private static IReadOnlyDictionary<string, string> Report(ScenarioResponse response) =>
        response.Text
            .Split(';')
            .Select(part => part.Split(':', 2))
            .ToDictionary(parts => parts[0], parts => parts.Length > 1 ? parts[1] : string.Empty);
}
