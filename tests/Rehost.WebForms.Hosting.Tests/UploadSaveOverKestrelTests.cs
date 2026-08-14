using System.Text;
using Shouldly;
using Xunit;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Hosting.Tests;

// The write half of an upload, from a page holding the posted content in memory. The spilled
// content path is the body fixture's, since only that application sets a threshold low enough to
// reach it.
public sealed class UploadSaveOverKestrelTests(PostbackLiveScenario scenario)
    : IClassFixture<PostbackLiveScenario>
{
    private static readonly byte[] Content = Encoding.UTF8.GetBytes("hello upload");

    [Fact]
    public async Task Saves_An_Uploaded_File_To_A_Path_Rooted_On_This_Platform()
    {
        using var directory = new TempDirectory();
        var target = directory.Path("photo.jpg");

        var response = await UploadAsync(Content, target);

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("<p id=\"save-report\">saved</p>");
        File.ReadAllBytes(target).ShouldBe(Content);
    }

    // WriteTo skips a zero-length body, so the file has to be created by opening it rather than by
    // writing to it.
    [Fact]
    public async Task Saves_An_Empty_Uploaded_File_As_An_Empty_File()
    {
        using var directory = new TempDirectory();
        var target = directory.Path("empty.txt");

        var response = await UploadAsync([], target);

        response.Text.ShouldContain("<p id=\"save-report\">saved</p>");
        File.ReadAllBytes(target).ShouldBeEmpty();
    }

    // requireRootedSaveAsPath defaults to true, and the fixture does not turn it off.
    [Fact]
    public async Task Refuses_A_Relative_Path_While_Rooted_Paths_Are_Required()
    {
        var response = await UploadAsync(Content, "uploads/photo.jpg");

        response.Text.ShouldContain(
            "<p id=\"save-report\">error:System.Web.HttpException:The SaveAs method is configured"
            + " to require a rooted path, and the path 'uploads/photo.jpg' is not rooted.</p>");
    }

    // Windows keeps Framework's behavior exactly; elsewhere the path cannot name a file at all, so
    // it is refused by its real problem rather than by the generic rooted-path message — or, with
    // the guard off, silently written to a file whose name is the whole path.
    [Fact]
    public async Task Refuses_A_Windows_Path_Where_It_Cannot_Be_Rooted()
    {
        if (OperatingSystem.IsWindows())
        {
            using var directory = new TempDirectory();
            var target = directory.Path("photo.jpg");

            var accepted = await UploadAsync(Content, target);

            accepted.Text.ShouldContain("<p id=\"save-report\">saved</p>");
            File.ReadAllBytes(target).ShouldBe(Content);
            return;
        }

        var response = await UploadAsync(Content, @"C:\uploads\photo.jpg");

        response.Text.ShouldContain(
            "<p id=\"save-report\">error:System.Web.HttpException:The SaveAs path"
            + @" 'C:\uploads\photo.jpg' is rooted only on Windows, and this process is not running"
            + " on Windows. Supply a path rooted on this platform, or build one with"
            + " Server.MapPath.</p>");
    }

    private async Task<ScenarioResponse> UploadAsync(byte[] content, string target)
    {
        var html = (await scenario.Client.GetAsync("/Upload.aspx")).Text;

        var fields = PostbackForm.Fields(html);
        fields["Note"] = "a note";
        fields["Save"] = "Save";

        return await scenario.Client.PostAsync(
            PostbackForm.Action(html) + "?to=" + Uri.EscapeDataString(target),
            PostbackForm.EncodeMultipart(
                fields,
                [new MultipartFile("Picked", "photo.jpg", "image/jpeg", content)]),
            "multipart/form-data; boundary=" + PostbackForm.MultipartBoundary);
    }
}
