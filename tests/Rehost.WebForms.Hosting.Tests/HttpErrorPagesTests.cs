using System.Net;
using System.Text;
using System.Web.IisConfig;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Rehost.WebForms.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// What IIS's custom-error module did with the response an application left: the error mode and
// the client decide whether the detailed body is written at all, and existingResponse with
// TrySkipIisCustomErrors decide whether a row replaces what the application wrote.
public sealed class HttpErrorPagesTests
{
    private const string BuiltIn404 = """
        <!DOCTYPE html>
        <html>
        <head><title>404 - File or directory not found.</title></head>
        <body>
        <h1>Server Error</h1>
        <h2>404 - File or directory not found.</h2>
        <p>The resource you are looking for has been removed, had its name changed, or is temporarily unavailable.</p>

        </body>
        </html>
        """;

    private const string ApplicationBody = "the page's own body";

    [Theory]
    // Detailed for this client: the empty entity is filled, anything the application wrote stays.
    [InlineData("DetailedLocalOnly", true, "Auto", true, false, false)]
    [InlineData("DetailedLocalOnly", true, "Auto", false, false, true)]
    [InlineData("DetailedLocalOnly", true, "Auto", true, true, false)]
    [InlineData("Detailed", false, "Auto", false, false, true)]
    // Custom for this client: the row replaces what the application wrote unless it asked to skip.
    [InlineData("DetailedLocalOnly", false, "Auto", true, false, true)]
    [InlineData("Custom", true, "Auto", true, false, true)]
    [InlineData("Custom", true, "Auto", true, true, false)]
    [InlineData("Custom", true, "Auto", false, false, true)]
    // PassThrough sends the existing response even when it is empty; Replace outranks the flag.
    [InlineData("Custom", true, "PassThrough", true, false, false)]
    [InlineData("Custom", true, "PassThrough", false, false, false)]
    [InlineData("Custom", true, "Replace", true, true, true)]
    public async Task The_Mode_The_Client_And_The_Skip_Flag_Decide_The_Entity(
        string mode,
        bool local,
        string existingResponse,
        bool hasBody,
        bool skip,
        bool replaced)
    {
        var answer = await AnswerAsync(
            Config(
                Enum.Parse<HttpErrorMode>(mode),
                Enum.Parse<ExistingResponse>(existingResponse)),
            body: hasBody ? ApplicationBody : null,
            skip: skip,
            local: local);

        answer.Status.ShouldBe(404);
        answer.Body.ShouldBe(
            replaced ? BuiltIn404.ReplaceLineEndings("\n") : hasBody ? ApplicationBody : "");
        answer.Header("Connection").ShouldBe("close");
    }

    [Fact]
    public async Task A_File_Row_Delivers_The_Files_Bytes_And_Its_Configured_Type()
    {
        var page = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.WriteAllText(page, """{"error":"gone"}""");

        try
        {
            var answer = await AnswerAsync(
                Config(
                    HttpErrorMode.Custom,
                    ExistingResponse.Auto,
                    new HttpErrorRow(404, -1, HttpErrorRowMode.File, page, "application/json", null)),
                body: ApplicationBody);

            answer.Status.ShouldBe(404);
            answer.Body.ShouldBe("""{"error":"gone"}""");
            answer.Header("Content-Type").ShouldBe("application/json");
            answer.ContentLength.ShouldBe(16);
        }
        finally
        {
            File.Delete(page);
        }
    }

    [Fact]
    public async Task A_File_Row_Whose_File_Is_Gone_Falls_Back_To_The_Built_In_Body()
    {
        var answer = await AnswerAsync(
            Config(
                HttpErrorMode.Custom,
                ExistingResponse.Auto,
                new HttpErrorRow(
                    404,
                    -1,
                    HttpErrorRowMode.File,
                    Path.Combine(Path.GetTempPath(), "rehost-no-such-error-page.htm"),
                    "text/html",
                    null)),
            body: ApplicationBody);

        answer.Status.ShouldBe(404);
        answer.Body.ShouldBe(BuiltIn404.ReplaceLineEndings("\n"));
        answer.Header("Content-Type").ShouldBe("text/html");
    }

    [Fact]
    public async Task A_Redirect_Row_Answers_The_Absolute_Location_And_The_Object_Moved_Body()
    {
        var answer = await AnswerAsync(
            Config(
                HttpErrorMode.Custom,
                ExistingResponse.Auto,
                new HttpErrorRow(404, -1, HttpErrorRowMode.Redirect, null, null, "/gone.aspx")),
            body: ApplicationBody);

        answer.Status.ShouldBe(302);
        answer.ReasonPhrase.ShouldBe("Redirect");
        answer.Header("Location").ShouldBe("http://example.invalid/gone.aspx");
        answer.Header("Content-Type").ShouldBe("text/html; charset=UTF-8");
        answer.Body.ShouldBe(
            """
            <head><title>Document Moved</title></head>
            <body><h1>Object Moved</h1>This document may be found <a href="http://example.invalid/gone.aspx">here</a></body>
            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task A_Status_Below_400_Is_Left_Alone()
    {
        var answer = await AnswerAsync(
            Config(
                HttpErrorMode.Custom,
                ExistingResponse.Replace,
                new HttpErrorRow(302, -1, HttpErrorRowMode.BuiltIn, null, null, null)),
            status: 302,
            body: ApplicationBody);

        answer.Status.ShouldBe(302);
        answer.Body.ShouldBe(ApplicationBody);
    }

    private static HttpErrors Config(
        HttpErrorMode mode, ExistingResponse existingResponse, params HttpErrorRow[] rows) =>
        new(mode, existingResponse, rows);

    private static async Task<Answer> AnswerAsync(
        HttpErrors config,
        int status = 404,
        string? body = null,
        bool skip = false,
        bool local = true)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("example.invalid");
        context.Connection.LocalIpAddress = IPAddress.Parse("10.1.1.1");
        context.Connection.RemoteIpAddress =
            local ? IPAddress.Loopback : IPAddress.Parse("203.0.113.7");
        using var delivered = new MemoryStream();
        context.Response.Body = delivered;

        using var spool = new ResponseSpool(Path.GetTempPath);
        spool.SetStatus(status, "Not Found");
        spool.AddHeader("Connection", "close");
        if (body != null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            spool.AddHeader("Content-Type", "text/plain");
            spool.SetContentLength(bytes.Length);
            spool.Write(bytes, bytes.Length);
        }

        spool.BeforeHeadCommit = (spooled, spooledContext) =>
            HttpErrorPages.Apply(spooled, spooledContext, config, skip);
        spool.Seal();
        await spool.CommitAsync(context, TestContext.Current.CancellationToken);

        return new Answer(
            context.Response.StatusCode,
            context.Features.Get<IHttpResponseFeature>()!.ReasonPhrase ?? "",
            Encoding.UTF8.GetString(delivered.ToArray()),
            context.Response.Headers,
            context.Response.ContentLength);
    }

    private sealed record Answer(
        int Status,
        string ReasonPhrase,
        string Body,
        IHeaderDictionary Headers,
        long? ContentLength)
    {
        internal string? Header(string name) =>
            Headers.TryGetValue(name, out var values) ? values.ToString() : null;
    }
}
