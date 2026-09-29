using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Rehost.Web.AspNetCore;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// The rewrite step the host runs before the managed pipeline, against the measured IIS answers
// rather than the importer's own.
public sealed class RewriteRulesTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-rewriterules-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public async Task A_Rewrite_Puts_The_Rules_Query_Before_The_Requests()
    {
        var context = Context("/clean/5", "?extra=9&more=2");

        var outcome = await Rules(CleanRule).ApplyAsync(context);

        outcome.Kind.ShouldBe(RewriteOutcomeKind.Rewritten);
        outcome.OriginalUrl.ShouldBe("/clean/5?extra=9&more=2");
        context.Request.Path.Value.ShouldBe("/probe.aspx");
        context.Request.QueryString.Value.ShouldBe("?id=5&extra=9&more=2");
    }

    [Fact]
    public async Task A_Pattern_Matches_Whatever_The_Client_Capitalized()
    {
        var context = Context("/CLEAN/5");

        var outcome = await Rules(CleanRule).ApplyAsync(context);

        outcome.Kind.ShouldBe(RewriteOutcomeKind.Rewritten);
        context.Request.QueryString.Value.ShouldBe("?id=5");
    }

    [Fact]
    public async Task An_Unmatched_Request_Keeps_Its_Url()
    {
        var context = Context("/elsewhere", "?a=1");

        var outcome = await Rules(CleanRule).ApplyAsync(context);

        outcome.Kind.ShouldBe(RewriteOutcomeKind.Unchanged);
        outcome.OriginalUrl.ShouldBeNull();
        context.Request.Path.Value.ShouldBe("/elsewhere");
        context.Request.QueryString.Value.ShouldBe("?a=1");
    }

    [Fact]
    public async Task The_Original_Url_Header_Replaces_Whatever_The_Client_Sent()
    {
        var context = Context("/clean/5", "?extra=9");
        context.Request.Headers["X-Original-URL"] = "/spoof";

        await Rules(CleanRule).ApplyAsync(context);

        context.Request.Headers["X-Original-URL"].ToString().ShouldBe("/clean/5?extra=9");
    }

    [Fact]
    public async Task An_Encoded_Path_Reaches_The_Header_Re_Encoded_And_The_Raw_Url_Decoded()
    {
        var context = Context("/clean/a b", "?x=1");

        var outcome = await Rules(CleanRule).ApplyAsync(context);

        outcome.OriginalUrl.ShouldBe("/clean/a b?x=1");
        context.Request.Headers["X-Original-URL"].ToString().ShouldBe("/clean/a%20b?x=1");
    }

    [Fact]
    public async Task A_Redirect_Answers_With_An_Absolute_Location_And_The_Iis_Reason_And_Body()
    {
        var context = Context("/old", "?k=1");
        var body = Recorded(context);

        var outcome = await Rules(
            """<rule name="r"><match url="^old$" /><action type="Redirect" url="new/thing" redirectType="Found" /></rule>""")
            .ApplyAsync(context);

        outcome.Kind.ShouldBe(RewriteOutcomeKind.Answered);
        context.Response.StatusCode.ShouldBe(302);
        context.Features.Get<IHttpResponseFeature>()!.ReasonPhrase.ShouldBe("Redirect");
        context.Response.Headers.Location.ToString()
            .ShouldBe("http://localhost:5000/new/thing?k=1");
        Text(body).ShouldContain("Object Moved", Case.Sensitive);
        Text(body).ShouldContain("http://localhost:5000/new/thing?k=1", Case.Sensitive);
    }

    [Fact]
    public async Task A_Custom_Response_Answers_With_Its_Reason_And_Iis_Stock_Body()
    {
        var context = Context("/denied");
        var body = Recorded(context);

        var outcome = await Rules(
            """
            <rule name="c">
              <match url="^denied$" />
              <action type="CustomResponse" statusCode="403" statusReason="Nope" statusDescription="Local detail only" />
            </rule>
            """)
            .ApplyAsync(context);

        outcome.Kind.ShouldBe(RewriteOutcomeKind.Answered);
        context.Response.StatusCode.ShouldBe(403);
        context.Features.Get<IHttpResponseFeature>()!.ReasonPhrase.ShouldBe("Nope");
        Text(body).ShouldContain(
            "You do not have permission to view this directory or page.", Case.Sensitive);
        Text(body).ShouldNotContain("Local detail only");
    }

    [Fact]
    public async Task An_Abort_Rule_Drops_The_Connection()
    {
        var context = Context("/gone");
        var lifetime = new AbortRecorder();
        context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);

        var outcome = await Rules(
            """<rule name="a"><match url="^gone$" /><action type="AbortRequest" /></rule>""")
            .ApplyAsync(context);

        outcome.Kind.ShouldBe(RewriteOutcomeKind.Answered);
        lifetime.Aborted.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task A_Directory_Condition_Sees_A_Real_Folder()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "sub"));
        var rules = Rules(
            """
            <rule name="d">
              <match url="(.*)" />
              <conditions><add input="{REQUEST_FILENAME}" matchType="IsDirectory" /></conditions>
              <action type="Rewrite" url="folder.aspx?at={R:1}" />
            </rule>
            """);

        (await rules.ApplyAsync(Context("/nosuchfolder"))).Kind.ShouldBe(RewriteOutcomeKind.Unchanged);

        var context = Context("/sub");
        (await rules.ApplyAsync(context)).Kind.ShouldBe(RewriteOutcomeKind.Rewritten);
        context.Request.Path.Value.ShouldBe("/folder.aspx");
    }

    [Fact]
    public void A_Server_Variable_The_Parser_Refuses_Names_The_File()
    {
        var failure = Should.Throw<InvalidOperationException>(() => Rules(
            """
            <rule name="v">
              <match url="(.*)" />
              <conditions><add input="{HTTP_X_FORWARDED_PROTO}" pattern="^https$" /></conditions>
              <action type="Rewrite" url="secure.aspx" />
            </rule>
            """));

        failure.Message.ShouldContain(Path.Combine(_root.FullName, "web.config"));
        failure.Message.ShouldContain("HTTP_X_FORWARDED_PROTO", Case.Sensitive);
    }

    private const string CleanRule =
        """<rule name="clean"><match url="^clean/(.*)$" /><action type="Rewrite" url="probe.aspx?id={R:1}" /></rule>""";

    private RewriteRules Rules(string rules) => RewriteRules.Load(
        $"<rewrite><rules>{rules}</rules></rewrite>",
        Path.Combine(_root.FullName, "web.config"),
        _root.FullName);

    private static DefaultHttpContext Context(string path, string query = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost:5000");
        context.Request.Path = new PathString(path);
        context.Request.QueryString = new QueryString(query);
        return context;
    }

    private static MemoryStream Recorded(HttpContext context)
    {
        var body = new MemoryStream();
        context.Response.Body = body;
        return body;
    }

    private static string Text(MemoryStream body) => Encoding.UTF8.GetString(body.ToArray());

    private sealed class AbortRecorder : IHttpRequestLifetimeFeature
    {
        internal bool Aborted { get; private set; }

        public CancellationToken RequestAborted { get; set; }

        public void Abort() => Aborted = true;
    }
}
