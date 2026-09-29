using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

public sealed class WebApiOverKestrelTests(FriendlyUrlsLiveScenario scenario)
    : IClassFixture<FriendlyUrlsLiveScenario>
{
    private const string Json = "application/json; charset=utf-8";
    private const string SessionCookie = @"^ASP\.NET_SessionId=[a-z0-5]{24}; path=/; HttpOnly; SameSite=Lax$";

    [Fact]
    public async Task AppCodeControllerAnswersGetWithJsonAndNoCacheHeaders()
    {
        var response = await scenario.Client.GetAsync("/api/ping");

        response.StatusCode.ShouldBe(200, response.Text);
        response.ContentType.ShouldBe(Json);
        response.Text.ShouldBe("""{"pong":true}""");
        response.Headers["Cache-Control"].ShouldBe("no-cache");
        response.Headers["Pragma"].ShouldBe("no-cache");
        response.Headers["Expires"].ShouldBe("-1");
    }

    [Fact]
    public async Task PostBindsTheJsonBodyToTheActionModel()
    {
        var response = await scenario.Client.PostAsync(
            "/api/ping",
            Encoding.UTF8.GetBytes("""{"name":"x"}"""),
            "application/json");

        response.StatusCode.ShouldBe(200, response.Text);
        response.ContentType.ShouldBe(Json);
        response.Text.ShouldBe("""{"echo":"x"}""");
    }

    [Fact]
    public async Task UnknownControllerAnswersWebApisJson404()
    {
        var response = await scenario.Client.GetAsync("/api/missing");
        var uri = new Uri(scenario.Address, "/api/missing").AbsoluteUri;

        response.StatusCode.ShouldBe(404, response.Text);
        response.ContentType.ShouldBe(Json);
        response.Text.ShouldBe(
            $$"""{"Message":"No HTTP resource was found that matches the request URI '{{uri}}'.","MessageDetail":"No type was found that matches the controller named 'missing'."}""");
    }

    [Fact]
    public async Task SwappedRouteHandlerGivesControllersSessionState()
    {
        var set = await scenario.Client.GetAsync("/api/session?set=v");
        var cookie = set.SetCookies.ShouldHaveSingleItem();
        var read = await scenario.Client.GetWithCookiesAsync("/api/session", cookie.Split(';')[0]);
        var fresh = await scenario.Client.GetAsync("/api/session");

        set.StatusCode.ShouldBe(200, set.Text);
        set.ContentType.ShouldBe(Json);
        set.Text.ShouldBe("""{"value":"v"}""");
        cookie.ShouldMatch(SessionCookie);
        read.StatusCode.ShouldBe(200, read.Text);
        read.Text.ShouldBe("""{"value":"v"}""");
        read.SetCookies.ShouldBeEmpty();
        fresh.StatusCode.ShouldBe(200, fresh.Text);
        fresh.Text.ShouldBe("""{"value":null}""");
        fresh.SetCookies.ShouldBeEmpty();
    }

    [Fact]
    public async Task ThrowingActionAnswersWebApisJsonErrorWithDetail()
    {
        var response = await scenario.Client.GetAsync("/api/ping?fail=1");

        response.StatusCode.ShouldBe(500, response.Text);
        response.ContentType.ShouldBe(Json);
        using var error = JsonDocument.Parse(response.Bytes);
        error.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["Message", "ExceptionMessage", "ExceptionType", "StackTrace"]);
        error.RootElement.GetProperty("Message").GetString().ShouldBe("An error has occurred.");
        error.RootElement.GetProperty("ExceptionMessage").GetString().ShouldBe("probe-failure");
        error.RootElement.GetProperty("ExceptionType").GetString().ShouldBe("System.InvalidOperationException");
    }
}
