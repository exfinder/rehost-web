using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

public sealed class MvcOverKestrelTests(MvcLiveScenario scenario)
    : IClassFixture<MvcLiveScenario>
{
    private const string VersionHeader = "X-AspNetMvc-Version";

    [Fact]
    public async Task HomeRendersInsideItsLayoutWithTheMvcHeader()
    {
        var response = await scenario.Client.GetAsync("/");

        response.StatusCode.ShouldBe(200, response.Text);
        response.ContentType.ShouldBe("text/html; charset=utf-8");
        response.Header(VersionHeader).ShouldBe("5.3");
        response.Text.ShouldBe(
            """
            <!DOCTYPE html>
            <html>
            <head><title>Home - MVC fixture</title></head>
            <body>

            <h1 id="home">home-index</h1>

            <footer>Home/Index</footer>
            </body>
            </html>

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task ViewsFolderIsBlocked()
    {
        var response = await scenario.Client.GetAsync("/Views/Shared/_Layout.cshtml");

        response.StatusCode.ShouldBe(404, response.Text);
        response.Header(VersionHeader).ShouldBeNull();
        response.Text.ShouldContain("<title>The resource cannot be found.</title>", Case.Sensitive);
    }

    [Fact]
    public async Task CshtmlOutsideViewsIsForbidden()
    {
        var response = await scenario.Client.GetAsync("/loose.cshtml");

        response.StatusCode.ShouldBe(403, response.Text);
        response.Header(VersionHeader).ShouldBeNull();
        response.Text.ShouldContain("<title>This type of page is not served.</title>", Case.Sensitive);
        response.Text.ShouldContain(
            "[HttpException]: Path &#39;/loose.cshtml&#39; is forbidden.", Case.Sensitive);
    }

    [Fact]
    public async Task UnknownControllerAnswersMvcsNotFound()
    {
        var response = await scenario.Client.GetAsync("/Nope");

        response.StatusCode.ShouldBe(404, response.Text);
        response.Text.ShouldContain(
            "[HttpException]: The controller for path &#39;/Nope&#39; was not found or does not implement IController.",
            Case.Sensitive);
    }

    [Fact]
    public async Task ThrowingActionAnswersTheAspNetErrorPage()
    {
        var response = await scenario.Client.GetAsync("/Error");

        response.StatusCode.ShouldBe(500, response.Text);
        response.Text.ShouldContain("<title>mvc-fixture-failure</title>", Case.Sensitive);
        response.Text.ShouldContain("[InvalidOperationException: mvc-fixture-failure]", Case.Sensitive);
    }

    [Fact]
    public async Task AreaRouteServesTheAreaView()
    {
        var response = await scenario.Client.GetAsync("/Admin/Dashboard");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Header(VersionHeader).ShouldBe("5.3");
        response.Text.ShouldBe("<p id=\"admin\">admin-dashboard Admin</p>\n");
    }

    [Fact]
    public async Task LinqBinaryRendersAsBase64AndBindsBack()
    {
        var form = await scenario.Client.GetAsync("/Binary");

        form.StatusCode.ShouldBe(200, form.Text);
        form.Text.ShouldBe(
            """


            <form action="/Binary" method="post"><input id="Payload" name="Payload" type="hidden" value="AAH+/ysv" /><input id="Mirror" name="Mirror" type="hidden" value="AAH+/ysv" /></form>
            """.ReplaceLineEndings("\n"));

        var echo = await scenario.Client.PostFormAsync("/Binary", "Payload=AAH%2B%2Fysv");

        echo.StatusCode.ShouldBe(200, echo.Text);
        echo.Text.ShouldBe("0001FEFF2B2F");
    }

    [Fact]
    public async Task EntityStatePropertiesAreLeftOutOfObjectTemplates()
    {
        var response = await scenario.Client.GetAsync("/Binary/State");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldContain("state-name", Case.Sensitive);
        response.Text.ShouldNotContain("Modified");
    }

    [Fact]
    public async Task ChildActionOutputIsCached()
    {
        var first = await scenario.Client.GetAsync("/Home/Cached");
        var second = await scenario.Client.GetAsync("/Home/Cached");

        first.StatusCode.ShouldBe(200, first.Text);
        second.Text.ShouldBe(first.Text);
        first.Text.ShouldMatch("""^\n\n<p id="first">([0-9a-f]{32})</p>\n<p id="second">\1</p>\n$""");
    }
}
