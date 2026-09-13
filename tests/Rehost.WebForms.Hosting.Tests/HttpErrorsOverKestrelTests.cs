using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The wiring only a live host proves: the runtime leaves a native refusal's entity empty, the
// spool's head commit fills it from the application's row, and the configured response headers
// still ride the page that replaced it.
public sealed class HttpErrorsOverKestrelTests(CustomErrorsLiveScenario scenario)
    : IClassFixture<CustomErrorsLiveScenario>
{
    [Fact]
    public async Task A_Missing_Static_File_Answers_The_Rows_File_With_Its_Own_Type()
    {
        var response = await scenario.Client.GetAsync("/nosuch-error-page.json");

        response.StatusCode.ShouldBe(404);
        response.Header("Content-Type").ShouldBe("application/json");
        response.Text.ShouldBe("""{"status":404,"from":"httpErrors"}""");
        response.Header("X-Powered-By").ShouldBe("ASP.NET");
    }
}
