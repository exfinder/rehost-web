using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The client renders the page, scrapes the form it rendered, and posts that back over a real
// socket, so nothing here replays a recorded body.
public sealed class PostbackOverKestrelTests
{
    [Fact]
    public void Renders_A_Server_Form_Carrying_Protected_State_Fields()
    {
        using var run = ScenarioRun.Postback("apply");

        var render = run.ResponseText(0);

        render.ShouldContain("name=\"__VIEWSTATE\"");
        render.ShouldContain("name=\"__EVENTVALIDATION\"");

        // Pinned, not merely present: GetClientStateIdentifier hashes the template source
        // directory and the generated type name, and ledger P38 makes that hash the stable
        // algorithm rather than the per-process randomized one. A randomized hash would differ
        // on every run and between platforms. The value cannot agree with .NET Framework's,
        // which reaches StringComparer.InvariantCultureIgnoreCase.GetHashCode instead.
        render.ShouldContain("name=\"__VIEWSTATEGENERATOR\" id=\"__VIEWSTATEGENERATOR\" value=\"72DAA2F9\"");
        render.ShouldContain(
            "<p id=\"restored\">postback=False|posted-viewstate=False|clicks=0"
            + "|note=note-from-initial|carried=carried-from-initial|message=|form=|echo=</p>");
    }

    [Fact]
    public void Round_Trips_Form_Values_And_View_State_Across_A_Postback()
    {
        using var run = ScenarioRun.Postback("apply");

        run.Trace.ShouldContain("request:apply:postback:200");
        run.ResponseText(1).ShouldContain(
            "<p id=\"restored\">postback=True|posted-viewstate=True|clicks=1|note="
            + "|carried=carried-from-initial|message=typed by the client"
            + "|form=typed by the client|echo=applied:typed by the client</p>");
    }

    // Two rounds, because one click reaches clicks=1 whether or not anything was restored. The
    // second round separates the mechanisms too: Clicks accumulates while Note stays lost.
    [Fact]
    public void Control_State_Survives_A_Control_Whose_View_State_Is_Disabled()
    {
        using var run = ScenarioRun.Postback("apply-twice");

        run.ResponseText(0).ShouldContain("<span id=\"Ticker\">clicks=0 note=note-from-initial</span>");
        run.ResponseText(1).ShouldContain("<span id=\"Ticker\">clicks=1 note=</span>");
        run.ResponseText(2).ShouldContain("<span id=\"Ticker\">clicks=2 note=</span>");
    }

    [Fact]
    public void A_Postback_Changes_The_Rendered_Output()
    {
        using var run = ScenarioRun.Postback("apply");

        run.ResponseText(0).ShouldContain("<span id=\"Echo\"></span>");
        run.ResponseText(1).ShouldContain("<span id=\"Echo\">applied:typed by the client</span>");
    }

    // A LinkButton posts through __EVENTTARGET rather than by submitting its own name, so it
    // reaches RaisePostBackEvent by a different route than the Button.
    [Fact]
    public void An_Event_Target_Postback_Raises_The_Link_Button_Event()
    {
        using var run = ScenarioRun.Postback("bump");

        run.ResponseText(1).ShouldContain("<span id=\"Echo\">bumped</span>");
        run.ResponseText(1).ShouldContain("|clicks=1|");
    }

    // Changed events run after Load, not before it, which is the ordering ProcessRequestMain
    // fixes and the one an application's handlers depend on.
    [Fact]
    public void Postback_Events_Run_In_The_Framework_Order()
    {
        using var run = ScenarioRun.Postback("apply");

        run.ResponseText(1).ShouldContain(
            "<p id=\"trace\">page.init>counter.load-control-state>page.load.postback"
            + ">message.text-changed>apply.click>page.prerender"
            + ">counter.save-control-state</p>");
    }

    [Fact]
    public void A_Tampered_View_State_Fails_Mac_Validation()
    {
        using var run = ScenarioRun.Postback("tamper");

        run.Trace.ShouldContain("request:tamper:postback:500");
        run.ResponseText(1).ShouldContain("Validation of viewstate MAC failed");
    }

    // The same corrupt-payload class as the tampered case, distinguished only by the generator
    // field not naming this page: that one throws, this one is swallowed and the view state
    // dropped. A posted __VIEWSTATE with IsPostBack false is the signature of the suppression,
    // since a valid payload makes IsPostBack true and an unsuppressed invalid one throws.
    [Fact]
    public void A_View_State_From_Another_Page_Is_Suppressed_Rather_Than_Thrown()
    {
        using var run = ScenarioRun.Postback("cross-page");

        run.Trace.ShouldContain("request:cross-page:postback:200");
        run.ResponseText(2).ShouldContain("<p id=\"restored\">postback=False|posted-viewstate=True|");
    }

    [Fact]
    public void Request_Validation_Rejects_Dangerous_Form_Input()
    {
        using var run = ScenarioRun.Postback("unsafe-input");

        run.Trace.ShouldContain("request:unsafe-input:postback:500");
        run.ResponseText(1).ShouldContain("HttpRequestValidationException");
        run.ResponseText(1).ShouldContain("A potentially dangerous Request.Form value was detected");
    }
}
