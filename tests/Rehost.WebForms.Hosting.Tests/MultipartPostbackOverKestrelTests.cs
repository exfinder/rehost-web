using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Reading only. HttpPostedFile.SaveAs and its RequireRootedSaveAsPath check are a separate story:
// Path.IsPathRooted disagrees across operating systems, so that branch needs its own coverage.
public sealed class MultipartPostbackOverKestrelTests
{
    // note= comes from Request.Form, which on a multipart body is filled by the multipart branch
    // of FillInFormCollection rather than the urlencoded one, and postback=True means the view
    // state travelled as a multipart field.
    [Fact]
    public void Fills_The_Form_And_Files_Collections_From_A_Multipart_Postback()
    {
        using var run = ScenarioRun.Postback("upload");

        run.Trace.ShouldContain("request:upload:postback:200");
        run.ResponseText(1).ShouldContain(
            "<p id=\"upload-report\">postback=True|note=a note|files=1|key=Picked"
            + "|name=notes.txt|type=text/plain|length=12|content=hello upload|result=saved</p>");
    }

    [Fact]
    public void A_File_Part_With_No_Content_Is_A_File_Of_Zero_Length()
    {
        using var run = ScenarioRun.Postback("upload-empty");

        run.ResponseText(1).ShouldContain(
            "|files=1|key=Picked|name=empty.txt|type=text/plain|length=0|content=|");
    }

    // A browser posts a part with an empty filename when the input was left alone, and that still
    // counts as a file rather than as no file at all.
    [Fact]
    public void An_Untouched_File_Input_Still_Posts_A_File_Entry()
    {
        using var run = ScenarioRun.Postback("upload-none");

        run.ResponseText(1).ShouldContain(
            "|files=1|key=Picked|name=|type=application/octet-stream|length=0|content=|");
    }
}
