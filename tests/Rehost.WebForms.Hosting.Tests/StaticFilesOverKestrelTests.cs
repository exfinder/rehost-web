using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The conditional matrix a real client exercises against StaticFileHandler over this host
// (ledger P58): revalidation 304s (port-owned, the role IIS's native module played), the
// imported range arms, and HEAD suppression. styles.css is 34 bytes of fixture content.
public sealed class StaticFilesOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task If_Modified_Since_Answers_304_Without_A_Body()
    {
        var baseline = await scenario.Client.GetAsync("/styles.css");
        var lastModified = baseline.Header("Last-Modified");
        lastModified.ShouldNotBeNull();

        var revalidation = await scenario.Client.GetWithHeadersAsync(
            "/styles.css", ("If-Modified-Since", lastModified!));

        revalidation.StatusCode.ShouldBe(304);
        revalidation.Bytes.ShouldBeEmpty();
        revalidation.Header("ETag").ShouldBe(baseline.Header("ETag"));
    }

    [Fact]
    public async Task An_Older_If_Modified_Since_Serves_The_Full_File()
    {
        var response = await scenario.Client.GetWithHeadersAsync(
            "/styles.css", ("If-Modified-Since", "Sat, 01 Jan 2000 00:00:00 GMT"));

        response.StatusCode.ShouldBe(200);
        response.Bytes.Length.ShouldBe(34);
    }

    [Fact]
    public async Task If_None_Match_Answers_304_And_Wins_Over_If_Modified_Since()
    {
        var baseline = await scenario.Client.GetAsync("/styles.css");
        var etag = baseline.Header("ETag");
        etag.ShouldNotBeNull();

        var revalidation = await scenario.Client.GetWithHeadersAsync(
            "/styles.css",
            ("If-None-Match", etag!),
            ("If-Modified-Since", "Sat, 01 Jan 2000 00:00:00 GMT"));
        var mismatch = await scenario.Client.GetWithHeadersAsync(
            "/styles.css", ("If-None-Match", "\"different\""));

        revalidation.StatusCode.ShouldBe(304);
        revalidation.Bytes.ShouldBeEmpty();
        mismatch.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task A_Byte_Range_Answers_206_With_The_Requested_Slice()
    {
        var full = await scenario.Client.GetAsync("/styles.css");
        var response = await scenario.Client.GetWithHeadersAsync(
            "/styles.css", ("Range", "bytes=2-6"));

        response.StatusCode.ShouldBe(206);
        response.Header("Content-Range").ShouldBe("bytes 2-6/34");
        response.Bytes.ShouldBe(full.Bytes[2..7]);
    }

    [Fact]
    public async Task An_Unsatisfiable_Range_Answers_416_Naming_The_Length()
    {
        var response = await scenario.Client.GetWithHeadersAsync(
            "/styles.css", ("Range", "bytes=99999-"));

        response.StatusCode.ShouldBe(416);
        response.Header("Content-Range").ShouldBe("bytes */34");
    }

    // The extension gate (ledger P58): IIS's static content-type list decides what serves; an
    // off-list file beside the app's content must refuse, as IIS refused it.
    // Types come from IIS's map, as integrated mode took them, not the classic table
    // (.js is application/javascript there, and .svg exists at all).
    [Fact]
    public async Task An_Unmapped_Known_Extension_Serves_With_Its_Iis_Content_Type()
    {
        File.WriteAllText(
            Path.Combine(scenario.ApplicationPath, "gate-probe.js"), "var gate = 1;");
        File.WriteAllText(
            Path.Combine(scenario.ApplicationPath, "gate-probe.svg"), "<svg/>");

        var script = await scenario.Client.GetAsync("/gate-probe.js");
        var image = await scenario.Client.GetAsync("/gate-probe.svg");

        script.StatusCode.ShouldBe(200);
        script.Header("Content-Type").ShouldBe("application/javascript");
        script.Text.ShouldBe("var gate = 1;");
        image.StatusCode.ShouldBe(200);
        image.Header("Content-Type").ShouldBe("image/svg+xml");
    }

    [Fact]
    public async Task An_Off_List_Extension_Answers_404()
    {
        File.WriteAllText(
            Path.Combine(scenario.ApplicationPath, "gate-probe.bak"), "secret");

        var response = await scenario.Client.GetAsync("/gate-probe.bak");

        response.StatusCode.ShouldBe(404);
        response.Text.ShouldNotContain("secret");
    }

    [Fact]
    public async Task Head_Answers_The_Get_Headers_With_No_Body()
    {
        var response = await scenario.Client.HeadAsync("/styles.css");

        response.StatusCode.ShouldBe(200);
        response.Header("Content-Length").ShouldBe("34");
        response.Header("Content-Type").ShouldBe("text/css");
        response.Bytes.ShouldBeEmpty();
    }
}
