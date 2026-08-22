using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// One test per Response.Headers reading (H1-H16, IIS Express 10.0.26013), taken with the probe
// this handler reproduces: each asserts the wire and the collection the handler saw before the
// send. IIS-only artefacts of the readings (Server, X-SourceFiles, X-Powered-By, Date) are not
// this host's to produce. Ledger P68.
public sealed class ResponseHeadersOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task H1_A_Bare_Handler_Sends_The_Generated_Block_Over_An_Empty_Collection()
    {
        var response = await Probe("baseline");

        response.StatusCode.ShouldBe(200);
        response.Header("Content-Type").ShouldBe("text/html; charset=utf-8");
        response.Header("Cache-Control").ShouldBe("private");
        response.Header("X-AspNet-Version").ShouldBe("4.0.30319");
        response.Header("Location").ShouldBeNull();
        response.SetCookies.ShouldBeEmpty();
        response.Text.ShouldBe("Count=0\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
    }

    [Fact]
    public async Task H2_Setting_ContentType_Leaves_The_Collection_Empty()
    {
        var response = await Probe("contenttype");

        response.Header("Content-Type").ShouldBe("text/plain; charset=utf-8");
        response.Text.ShouldBe("Count=0\nContentType=text/plain\nRedirectLocation=null\nStatusCode=200\n");
    }

    [Fact]
    public async Task H3_Added_Cookies_Leave_The_Collection_Empty()
    {
        var one = await Probe("cookie");
        var two = await Probe("cookie2");

        one.SetCookies.ShouldBe(["a=1; path=/"]);
        two.SetCookies.ShouldBe(["a=1; path=/", "b=2; path=/"]);
        one.Text.ShouldBe("Count=0\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
        two.Text.ShouldBe("Count=0\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
    }

    [Fact]
    public async Task H4_Redirect_Leaves_The_Collection_Empty_And_Sends_From_The_Field()
    {
        var response = await Probe("redirect");

        response.StatusCode.ShouldBe(302);
        response.Header("Location").ShouldBe("/x");
        response.Text.ShouldEndWith("Count=0\nContentType=text/html\nRedirectLocation=/x\nStatusCode=302\n");
        response.Text.ShouldContain("Object moved");
    }

    [Fact]
    public async Task H5_Cache_Policy_Leaves_The_Collection_Empty()
    {
        var plain = await Probe("cache");
        var aged = await Probe("cachemax");

        plain.Header("Cache-Control").ShouldBe("public");
        aged.Header("Cache-Control").ShouldBe("public, max-age=30");
        plain.Text.ShouldBe("Count=0\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
        aged.Text.ShouldBe("Count=0\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
    }

    [Fact]
    public async Task H6_AppendHeader_Writes_Through_The_Collection_And_Both_Values_Are_Sent()
    {
        var response = await Probe("append");

        response.Header("X-Custom").ShouldBe("v1, v2");
        response.Text.ShouldBe(
            "Count=1\nH X-Custom=v1\nH X-Custom=v2\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
    }

    [Fact]
    public async Task H7_A_Collection_Location_Is_Sent_And_Never_Reaches_RedirectLocation()
    {
        var kept = await Probe("setloc");
        var moved = await Probe("setloc302");

        kept.StatusCode.ShouldBe(200);
        kept.Header("Location").ShouldBe("/y");
        moved.StatusCode.ShouldBe(302);
        moved.Header("Location").ShouldBe("/y");
        kept.Text.ShouldBe(
            "Count=1\nH Location=/y\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
        moved.Text.ShouldBe(
            "Count=1\nH Location=/y\nContentType=text/html\nRedirectLocation=null\nStatusCode=302\n");
    }

    [Fact]
    public async Task H8_A_Collection_Content_Type_Loses_To_The_Managed_Field()
    {
        var replaced = await Probe("setct");
        var appended = await Probe("addct");

        replaced.Header("Content-Type").ShouldBe("text/html; charset=utf-8");
        appended.Header("Content-Type").ShouldBe("text/html; charset=utf-8");
        replaced.Text.ShouldBe(
            "Count=1\nH Content-Type=text/csv\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
        appended.Text.ShouldBe(
            "Count=1\nH Content-Type=text/csv\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
    }

    [Fact]
    public async Task H9_A_Collection_Cache_Control_Loses_To_The_Cache_Policy()
    {
        var response = await Probe("setcc");

        response.Header("Cache-Control").ShouldBe("private");
        response.Text.ShouldBe(
            "Count=1\nH Cache-Control=no-store\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
    }

    [Fact]
    public async Task H10_Removing_Set_Cookie_Does_Not_Reach_The_Cookies()
    {
        var before = await Probe("rmcookie");
        var after = await Probe("rmcookie2");

        before.SetCookies.ShouldBe(["a=1; path=/"]);
        after.SetCookies.ShouldBe(["a=1; path=/", "b=2; path=/"]);
        before.Text.ShouldBe("Count=0\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
    }

    [Fact]
    public async Task H11_Removing_Location_Does_Not_Reach_RedirectLocation()
    {
        var response = await Probe("rmloc");

        response.StatusCode.ShouldBe(302);
        response.Header("Location").ShouldBe("/x");
        response.Text.ShouldEndWith("Count=0\nContentType=text/html\nRedirectLocation=/x\nStatusCode=302\n");
    }

    [Fact]
    public async Task H12_Removing_Headers_The_Collection_Never_Held_Changes_Nothing()
    {
        var generated = await Probe("rmcache");
        var contentType = await Probe("rmct");

        generated.Header("Cache-Control").ShouldBe("private");
        generated.Header("X-AspNet-Version").ShouldBe("4.0.30319");
        contentType.Header("Content-Type").ShouldBe("text/html; charset=utf-8");
        generated.Text.ShouldBe("Count=0\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
        contentType.Text.ShouldBe("Count=0\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
    }

    [Fact]
    public async Task H13_Removing_An_Appended_Header_Keeps_It_Off_The_Wire()
    {
        var response = await Probe("rmappend");

        response.Header("X-Custom").ShouldBeNull();
        response.Text.ShouldBe("Count=0\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
    }

    [Fact]
    public async Task H14_A_Collection_Set_Cookie_Is_Sent()
    {
        var response = await Probe("addsetcookie");

        response.SetCookies.ShouldBe(["z=9; path=/"]);
        response.Text.ShouldBe(
            "Count=1\nH Set-Cookie=z=9; path=/\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
    }

    // The port's block is generated in one shot, so the mirror holds every header this response
    // sent; IIS's native block at the same instant held only what it had already emitted, and
    // carried a Server header this host has none of.
    [Fact]
    public async Task H15_After_The_Flush_Add_Throws_Remove_Is_Inert_And_The_Collection_Mirrors_The_Sent_Block()
    {
        var response = await Probe("afterflush");

        response.Header("X-Custom").ShouldBe("v1");
        response.Text.ShouldBe(
            "flushed\n"
            + "late add: System.Web.HttpException: Server cannot append header after HTTP headers have been sent.\n"
            + "late remove: ok\n"
            + "late get: private\n"
            + "Count=5\n"
            + "H X-AspNet-Version=4.0.30319\n"
            + "H Transfer-Encoding=chunked\n"
            + "H X-Custom=v1\n"
            + "H Cache-Control=private\n"
            + "H Content-Type=text/html; charset=utf-8\n"
            + "ContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
    }

    [Fact]
    public async Task H16_ClearHeaders_Empties_The_Collection_And_The_Block_Is_Regenerated()
    {
        var response = await Probe("clear");

        response.Header("X-Custom").ShouldBeNull();
        response.Header("Cache-Control").ShouldBe("private");
        response.Header("X-AspNet-Version").ShouldBe("4.0.30319");
        response.Text.ShouldBe("Count=0\nContentType=text/html\nRedirectLocation=null\nStatusCode=200\n");
    }

    private async Task<ScenarioResponse> Probe(string probeCase) =>
        await scenario.Client.GetAsync(ProbePaths.Headers + "?case=" + probeCase);
}
