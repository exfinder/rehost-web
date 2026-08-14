using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Reading only. HttpPostedFile.SaveAs and its RequireRootedSaveAsPath check are a separate story:
// Path.IsPathRooted disagrees across operating systems, so that branch needs its own coverage.
public sealed class MultipartPostbackOverKestrelTests(PostbackLiveScenario scenario)
    : IClassFixture<PostbackLiveScenario>
{
    // note= comes from Request.Form, which on a multipart body is filled by the multipart branch
    // of FillInFormCollection rather than the urlencoded one, and postback=True means the view
    // state travelled as a multipart field.
    [Fact]
    public async Task Fills_The_Form_And_Files_Collections_From_A_Multipart_Postback()
    {
        var response = await UploadAsync(new MultipartFile(
            "Picked",
            "notes.txt",
            "text/plain",
            Encoding.UTF8.GetBytes("hello upload")));

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain(
            "<p id=\"upload-report\">postback=True|note=a note|files=1|key=Picked"
            + "|name=notes.txt|type=text/plain|length=12|content=hello upload|result=saved</p>");
    }

    [Fact]
    public async Task A_File_Part_With_No_Content_Is_A_File_Of_Zero_Length()
    {
        var response = await UploadAsync(new MultipartFile("Picked", "empty.txt", "text/plain", []));

        response.Text.ShouldContain(
            "|files=1|key=Picked|name=empty.txt|type=text/plain|length=0|content=|");
    }

    // A browser posts a part with an empty filename when the input was left alone, and that still
    // counts as a file rather than as no file at all.
    [Fact]
    public async Task An_Untouched_File_Input_Still_Posts_A_File_Entry()
    {
        var response = await UploadAsync(
            new MultipartFile("Picked", "", "application/octet-stream", []));

        response.Text.ShouldContain(
            "|files=1|key=Picked|name=|type=application/octet-stream|length=0|content=|");
    }

    private async Task<ScenarioResponse> UploadAsync(MultipartFile file)
    {
        var html = (await scenario.Client.GetAsync("/Upload.aspx")).Text;

        var fields = PostbackForm.Fields(html);
        fields["Note"] = "a note";
        fields["Save"] = "Save";

        return await scenario.Client.PostAsync(
            PostbackForm.Action(html),
            PostbackForm.EncodeMultipart(fields, [file]),
            "multipart/form-data; boundary=" + PostbackForm.MultipartBoundary);
    }
}
