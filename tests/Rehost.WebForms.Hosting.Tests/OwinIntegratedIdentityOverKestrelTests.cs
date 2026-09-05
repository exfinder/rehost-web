using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The two Katana paths the integrated identity opens (ADR 0013), driven from the fixture's own
// middleware: both promise IIS plumbing the host has none of, and both must fall back rather
// than fail the request.
public sealed class OwinIntegratedIdentityOverKestrelTests(FriendlyUrlsLiveScenario scenario)
    : IClassFixture<FriendlyUrlsLiveScenario>
{
    [Fact]
    public async Task DisablingCompressionFromMiddlewareRewritesTheCacheHeadersAndServes()
    {
        var untouched = await scenario.Client.GetAsync("/Echo");
        var disabled = await scenario.Client.GetAsync("/Echo?compression=off");

        untouched.StatusCode.ShouldBe(200, untouched.Text);
        untouched.Headers["Cache-Control"].ShouldBe("private");

        disabled.StatusCode.ShouldBe(200, disabled.Text);
        disabled.Headers["Cache-Control"].ShouldBe("no-cache");
        disabled.Text.ShouldBe(untouched.Text);
    }

    [Fact]
    public async Task MiddlewareReadingCallCancelledGetsALiveToken()
    {
        var response = await scenario.Client.GetAsync("/Echo?cancel=read");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Headers["X-Call-Cancelled"].ShouldBe("armed-no");
    }
}
