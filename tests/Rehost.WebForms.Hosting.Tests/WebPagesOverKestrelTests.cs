using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

public sealed class WebPagesOverKestrelTests(WebPagesLiveScenario scenario)
    : IClassFixture<WebPagesLiveScenario>
{
    private const string VersionHeader = "X-AspNetWebPages-Version";
    private const string Html = "text/html; charset=utf-8";

    [Fact]
    public async Task PageRendersInsideItsLayoutWithTheWebPagesHeader()
    {
        var response = await scenario.Client.GetAsync("/hello.cshtml?name=X");

        response.StatusCode.ShouldBe(200, response.Text);
        response.ContentType.ShouldBe(Html);
        response.Header("Cache-Control").ShouldBe("private");
        response.Header(VersionHeader).ShouldBe("3.0");
        response.Text.ShouldBe(HelloPage("X", "0||", "~/hello.cshtml"));
    }

    [Theory]
    [InlineData("/hello", "~/hello.cshtml")]
    [InlineData("/Hello", "~/Hello.cshtml")]
    [InlineData("/HELLO.CSHTML", "~/HELLO.CSHTML")]
    public async Task ExtensionlessAndRecasedUrlsServeTheCompiledPageUnderTheUrlsCasing(string url, string virtualPath)
    {
        (await scenario.Client.GetAsync("/hello.cshtml")).StatusCode.ShouldBe(200);

        var response = await scenario.Client.GetAsync(url);

        response.StatusCode.ShouldBe(200, response.Text);
        response.Header(VersionHeader).ShouldBe("3.0");
        response.Text.ShouldBe(HelloPage("world", "0||", virtualPath));
    }

    [Fact]
    public async Task SegmentsAfterThePageArriveAsUrlData()
    {
        var response = await scenario.Client.GetAsync("/hello/a/b");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Header(VersionHeader).ShouldBe("3.0");
        response.Text.ShouldBe(HelloPage("world", "2|a|b", "~/hello.cshtml"));
    }

    [Theory]
    [InlineData("/admin/")]
    [InlineData("/admin")]
    public async Task FolderServesItsIndexPageWithoutARedirect(string url)
    {
        var response = await scenario.Client.GetAsync(url);

        response.StatusCode.ShouldBe(200, response.Text);
        response.Header("Location").ShouldBeNull();
        response.Header(VersionHeader).ShouldBe("3.0");
        response.Text.ShouldBe(
            """
            <!DOCTYPE html>
            <html>
            <head><title>Admin</title></head>
            <body>

            <p id="admin">admin index ~/admin/index.cshtml</p>

            <footer id="footer">footer-arg</footer>

            </body>
            </html>

            """.ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData("/both/", "<p>cshtml-default ~/both/default.cshtml</p>\n")]
    [InlineData("/onlycshtml/", "<p>onlycshtml-default ~/onlycshtml/default.cshtml</p>\n")]
    public async Task FolderDefaultCshtmlWinsOverDefaultAspx(string url, string body)
    {
        var response = await scenario.Client.GetAsync(url);

        response.StatusCode.ShouldBe(200, response.Text);
        response.Header(VersionHeader).ShouldBe("3.0");
        response.Text.ShouldBe(body);
    }

    [Fact]
    public async Task MixedCaseIndexAndStartPageServeTheFolder()
    {
        var response = await scenario.Client.GetAsync("/mixed/");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Header(VersionHeader).ShouldBe("3.0");
        response.Text.ShouldBe("<i>mixed-start</i>\n<b>mixed-index</b>\n\n");
    }

    [Theory]
    [InlineData("/_Layout.cshtml")]
    [InlineData("/_Layout")]
    public async Task UnderscoreFilesAreRefused(string url)
    {
        var response = await scenario.Client.GetAsync(url);

        response.StatusCode.ShouldBe(404, response.Text);
        response.Header(VersionHeader).ShouldBeNull();
        response.Text.ShouldContain("<title>The resource cannot be found.</title>", Case.Sensitive);
        response.Text.ShouldContain(
            "[HttpException]: Files with leading underscores (&quot;_&quot;) cannot be served.", Case.Sensitive);
    }

    [Fact]
    public async Task MissingPageAnswersTheNullMessageNotFound()
    {
        var response = await scenario.Client.GetAsync("/missing.cshtml");

        response.StatusCode.ShouldBe(404, response.Text);
        response.Header(VersionHeader).ShouldBeNull();
        response.Text.ShouldContain("<title>The resource cannot be found.</title>", Case.Sensitive);
        response.Text.ShouldContain(
            "[HttpException]: Exception of type &#39;System.Web.HttpException&#39; was thrown.", Case.Sensitive);
    }

    [Fact]
    public async Task MissingExtensionlessUrlIsNotFound()
    {
        var response = await scenario.Client.GetAsync("/missing");

        response.StatusCode.ShouldBe(404, response.Text);
        response.Header(VersionHeader).ShouldBeNull();
    }

    [Theory]
    [InlineData("/validate.cshtml", "\n<p id=\"form\">System.Web.HttpRequestValidationException</p>\n<p id=\"unvalidated\">&lt;script&gt;</p>\n")]
    [InlineData("/notread.cshtml", "<p id=\"notread\">form-not-read</p>\n")]
    public async Task ValidationThrowsOnlyWhenThePageReadsTheValidatedForm(string url, string body)
    {
        var response = await scenario.Client.PostFormAsync(url, "x=%3Cscript%3E");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Header(VersionHeader).ShouldBe("3.0");
        response.Text.ShouldBe(body);
    }

    [Fact]
    public async Task CompileErrorShowsTheCshtmlSourceAndTheGeneratedCode()
    {
        var response = await scenario.Client.GetAsync("/broken.cshtml");

        response.StatusCode.ShouldBe(500, response.Text);
        response.Header(VersionHeader).ShouldBeNull();
        response.Text.ShouldContain("<title>Compilation Error</title>", Case.Sensitive);
        response.Text.ShouldContain(
            "<b> Compiler Error Message: </b>CS1525: Invalid expression term &#39;;&#39;", Case.Sensitive);
        response.Text.ShouldContain(
            """
            Line 1:  @{
            <font color=red>Line 2:      var x = ;
            </font>Line 3:  }
            Line 4:  &lt;p&gt;@x&lt;/p&gt;</pre>
            """.ReplaceLineEndings("\r\n"),
            Case.Sensitive);
        response.Text.ShouldMatch(@"broken\.cshtml\r\n +&nbsp;&nbsp; <b>Line:</b>  2\r\n");
        response.Text.ShouldMatch(@"#line 1 &quot;[^&]*broken\.cshtml&quot;");
    }

    [Fact]
    public async Task ParseErrorNamesTheVirtualPathAndLine()
    {
        var response = await scenario.Client.GetAsync("/unclosed.cshtml");

        response.StatusCode.ShouldBe(500, response.Text);
        response.Header(VersionHeader).ShouldBeNull();
        response.Text.ShouldContain("<title>Parser Error</title>", Case.Sensitive);
        response.Text.ShouldContain(
            "<b> Parser Error Message: </b>The foreach block is missing a closing &quot;}&quot; character.",
            Case.Sensitive);
        response.Text.ShouldContain(
            """
            <font color=red>Line 1:  @foreach (var i in new[] {1}) {
            </font>Line 2:  &lt;p&gt;@i&lt;/p&gt;
            """.ReplaceLineEndings("\r\n"),
            Case.Sensitive);
        response.Text.ShouldContain(
            "<b> Source File: </b> /unclosed.cshtml<b> &nbsp;&nbsp; Line: </b> 1", Case.Sensitive);
    }

    [Fact]
    public async Task DisabledFolderForbidsItsCshtml()
    {
        var response = await scenario.Client.GetAsync("/off/page.cshtml");

        response.StatusCode.ShouldBe(403, response.Text);
        response.Header(VersionHeader).ShouldBeNull();
        response.Text.ShouldContain("<title>This type of page is not served.</title>", Case.Sensitive);
        response.Text.ShouldContain(
            "[HttpException]: Path &#39;/off/page.cshtml&#39; is forbidden.", Case.Sensitive);
    }

    [Fact]
    public async Task DisabledFolderDoesNotRouteExtensionlessUrls()
    {
        var response = await scenario.Client.GetAsync("/off/page");

        response.StatusCode.ShouldBe(404, response.Text);
        response.Header(VersionHeader).ShouldBeNull();
    }

    [Fact]
    public async Task WebFormsPageRendersACompiledWidgetWithAnAnonymousModel()
    {
        var response = await scenario.Client.GetAsync("/WidgetHost.aspx");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Header(VersionHeader).ShouldBeNull();
        response.Text.ShouldContain(
            """
            <div id="zone">
            <div class="widget">
                <h4 id="wtitle">From Web Forms</h4>
                <ul>
                    <li><a>one</a></li>
                    <li><a>two</a></li>
                </ul>
            </div>
            </div>
            """.ReplaceLineEndings("\n"),
            Case.Sensitive);
    }

    [Fact]
    public async Task HeadAnswersThePagesLengthAndHeadersWithoutABody()
    {
        var response = await scenario.Client.HeadAsync("/hello.cshtml");

        response.StatusCode.ShouldBe(200);
        response.ContentType.ShouldBe(Html);
        response.Header("Content-Length").ShouldBe("534");
        response.Header("Cache-Control").ShouldBe("private");
        response.Header(VersionHeader).ShouldBe("3.0");
        response.Bytes.ShouldBeEmpty();
    }

    [Fact]
    public async Task StartPageWrapsThePageAndSharesPageData()
    {
        var response = await scenario.Client.GetAsync("/ps/page");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Header(VersionHeader).ShouldBe("3.0");
        response.Text.ShouldBe("\n<i>start-before</i>\n<b>page fromstart=yes</b>\n\n<i>start-after</i>\n");
    }

    private static string HelloPage(string name, string urlData, string virtualPath) =>
        $"""
        <!DOCTYPE html>
        <html>
        <head><title>Hello page</title></head>
        <body>

        <p id="greet">Hello, {name}!</p>
        <p id="enc">&lt;b&gt;encoded&lt;/b&gt;</p>
        <p id="raw"><b>raw</b></p>
        <ul id="loop"><li>1</li><li>2</li><li>3</li></ul>
        <p id="helper"><b>HI</b></p>
        <p id="fn">42</p>
        <p id="textbox"><input id="q" name="q" type="text" value="v" /></p>
        <p id="href">/x/y</p>
        <p id="appstate">app-start-ran</p>
        <p id="ispost">False</p>
        <p id="urldata">{urlData}</p>
        <p id="vpath">{virtualPath}</p>

        <footer id="footer">footer-arg</footer>

        </body>
        </html>

        """.ReplaceLineEndings("\n");
}
