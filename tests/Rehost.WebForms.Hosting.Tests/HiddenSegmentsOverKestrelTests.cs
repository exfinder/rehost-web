using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// IIS request filtering answered 404 for any path containing a hidden segment, at any depth and
// casing; the port applies the same list at Framework's path-validation step (ledger P59).
// Files are created here so each 404 is a refusal of content that genuinely exists.
public sealed class HiddenSegmentsOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    // The host is shared, and an App_Browsers folder left behind fails every later page
    // request that evaluates Request.Browser; the file and any folder created here go away
    // before the next test sees the application.
    private async Task<int> StatusOfExistingAsync(string relativePath, string requestPath)
    {
        var physical = Path.Combine(
            scenario.ApplicationPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(physical)!;
        var createdDirectory = !Directory.Exists(directory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(physical, "hidden-content");

        try
        {
            return (await scenario.Client.GetAsync(requestPath)).StatusCode;
        }
        finally
        {
            File.Delete(physical);
            if (createdDirectory)
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Every_Hidden_Segment_Refuses_Its_Existing_Files()
    {
        (await StatusOfExistingAsync("App_Data/data.csv", "/App_Data/data.csv")).ShouldBe(404);
        (await StatusOfExistingAsync("App_Code/note.txt", "/App_Code/note.txt")).ShouldBe(404);
        (await StatusOfExistingAsync(
            "App_GlobalResources/r.txt", "/App_GlobalResources/r.txt")).ShouldBe(404);
        (await StatusOfExistingAsync(
            "App_LocalResources/l.txt", "/App_LocalResources/l.txt")).ShouldBe(404);
        (await StatusOfExistingAsync(
            "App_WebReferences/w.txt", "/App_WebReferences/w.txt")).ShouldBe(404);
        (await StatusOfExistingAsync("App_Browsers/b.txt", "/App_Browsers/b.txt")).ShouldBe(404);
        (await StatusOfExistingAsync("bin/readme.txt", "/bin/readme.txt")).ShouldBe(404);
    }

    // Not written through the helper: this is the application's own web.config, the file the
    // refusal exists for. Overwriting it would reconfigure the running host.
    [Fact]
    public async Task The_Applications_Own_Web_Config_Is_Refused()
    {
        File.Exists(Path.Combine(scenario.ApplicationPath, "web.config")).ShouldBeTrue();

        (await scenario.Client.GetAsync("/web.config")).StatusCode.ShouldBe(404);
        (await scenario.Client.GetAsync("/WEB.CONFIG")).StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task The_Refusal_Ignores_Casing_And_Depth()
    {
        (await StatusOfExistingAsync("App_Data/deep.csv", "/APP_DATA/deep.csv")).ShouldBe(404);
        (await StatusOfExistingAsync(
            "nested/App_Data/n.csv", "/nested/App_Data/n.csv")).ShouldBe(404);
    }

    [Fact]
    public async Task A_Name_Merely_Containing_A_Segment_Still_Serves()
    {
        var status = await StatusOfExistingAsync(
            "App_Data_Export/open.csv", "/App_Data_Export/open.csv");

        status.ShouldBe(200);
    }
}
