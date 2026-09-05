using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Each test renders the page, scrapes the form it rendered, and posts that back over a real socket,
// so nothing here replays a recorded body.
public sealed class PostbackOverKestrelTests(PostbackLiveScenario scenario)
    : IClassFixture<PostbackLiveScenario>
{
    private const string Message = "typed by the client";

    [Fact]
    public async Task Renders_A_Server_Form_Carrying_Protected_State_Fields()
    {
        var render = await scenario.Client.GetAsync("/Default.aspx");

        render.StatusCode.ShouldBe(200);
        render.Text.ShouldContain("name=\"__VIEWSTATE\"", Case.Sensitive);
        render.Text.ShouldContain("name=\"__EVENTVALIDATION\"", Case.Sensitive);

        // Pinned, not merely present: GetClientStateIdentifier hashes the template source
        // directory and the generated type name, and ledger P38 makes that hash the stable
        // algorithm rather than the per-process randomized one. A randomized hash would differ
        // on every run and between platforms. The value cannot agree with .NET Framework's,
        // which reaches StringComparer.InvariantCultureIgnoreCase.GetHashCode instead.
        render.Text.ShouldContain(
            "name=\"__VIEWSTATEGENERATOR\" id=\"__VIEWSTATEGENERATOR\" value=\"72DAA2F9\"");
        render.Text.ShouldContain(
            """<p id="restored">postback=False|posted-viewstate=False|clicks=0|note=note-from-initial|carried=carried-from-initial|message=|form=|echo=</p>""");
    }

    [Fact]
    public async Task Round_Trips_Form_Values_And_View_State_Across_A_Postback()
    {
        var response = await ApplyAsync(await RenderAsync());

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain(
            """<p id="restored">postback=True|posted-viewstate=True|clicks=1|note=|carried=carried-from-initial|message=""" + Message
            + "|form=" + Message + "|echo=applied:" + Message + "</p>");
    }

    // Two rounds, because one click reaches clicks=1 whether or not anything was restored. The
    // second round separates the mechanisms too: Clicks accumulates while Note stays lost.
    [Fact]
    public async Task Control_State_Survives_A_Control_Whose_View_State_Is_Disabled()
    {
        var render = await RenderAsync();
        render.ShouldContain("""<span id="Ticker">clicks=0 note=note-from-initial</span>""");

        var first = await ApplyAsync(render);
        first.Text.ShouldContain("""<span id="Ticker">clicks=1 note=</span>""");

        var second = await ApplyAsync(first.Text);
        second.Text.ShouldContain("""<span id="Ticker">clicks=2 note=</span>""");
    }

    [Fact]
    public async Task A_Postback_Changes_The_Rendered_Output()
    {
        var render = await RenderAsync();
        render.ShouldContain("""<span id="Echo"></span>""");

        var response = await ApplyAsync(render);

        response.Text.ShouldContain("""<span id="Echo">applied:""" + Message + "</span>");
    }

    // A LinkButton posts through __EVENTTARGET rather than by submitting its own name, so it
    // reaches RaisePostBackEvent by a different route than the Button.
    [Fact]
    public async Task An_Event_Target_Postback_Raises_The_Link_Button_Event()
    {
        var render = await RenderAsync();

        var response = await PostAsync(
            render,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Message"] = Message,
                ["__EVENTTARGET"] = "Bump",
                ["__EVENTARGUMENT"] = "",
            });

        response.Text.ShouldContain("""<span id="Echo">bumped</span>""");
        response.Text.ShouldContain("|clicks=1|");
    }

    // Changed events run after Load, not before it, which is the ordering ProcessRequestMain
    // fixes and the one an application's handlers depend on.
    [Fact]
    public async Task Postback_Events_Run_In_The_Framework_Order()
    {
        var response = await ApplyAsync(await RenderAsync());

        response.Text.ShouldContain(
            """<p id="trace">page.init>counter.load-control-state>page.load.postback>message.text-changed>apply.click>page.prerender>counter.save-control-state</p>""");
    }

    [Fact]
    public async Task A_Tampered_View_State_Fails_Mac_Validation()
    {
        var render = await RenderAsync();

        var response = await PostAsync(
            render,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Message"] = Message,
                ["Apply"] = "Apply",
                ["__VIEWSTATE"] =
                    FlipOneCharacter(PostbackForm.Fields(render)["__VIEWSTATE"]),
            });

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("Validation of viewstate MAC failed", Case.Sensitive);
    }

    // The same corrupt-payload class as the tampered case, distinguished only by the generator
    // field not naming this page: that one throws, this one is swallowed and the view state
    // dropped. A posted __VIEWSTATE with IsPostBack false is the signature of the suppression,
    // since a valid payload makes IsPostBack true and an unsuppressed invalid one throws.
    [Fact]
    public async Task A_View_State_From_Another_Page_Is_Suppressed_Rather_Than_Thrown()
    {
        var borrowed = PostbackForm.Fields(
            (await scenario.Client.GetAsync("/Other.aspx")).Text);
        var render = await RenderAsync();

        // Only the borrowed state travels. An event validation field is bound to the exact
        // __VIEWSTATE it was issued with, so carrying one would fail event validation before MAC
        // suppression could be observed, and posting no control values keeps ValidateEvent off
        // the path entirely.
        var response = await scenario.Client.PostFormAsync(
            PostbackForm.Action(render),
            PostbackForm.Encode(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["__VIEWSTATE"] = borrowed["__VIEWSTATE"],
                ["__VIEWSTATEGENERATOR"] = borrowed["__VIEWSTATEGENERATOR"],
                ["__EVENTTARGET"] = "",
                ["__EVENTARGUMENT"] = "",
            }));

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("""<p id="restored">postback=False|posted-viewstate=True|""");
    }

    [Fact]
    public async Task Request_Validation_Rejects_Dangerous_Form_Input()
    {
        var render = await RenderAsync();

        var response = await PostAsync(
            render,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Message"] = "<script>alert(1)</script>",
                ["Apply"] = "Apply",
            });

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("HttpRequestValidationException", Case.Sensitive);
        response.Text.ShouldContain("A potentially dangerous Request.Form value was detected", Case.Sensitive);
    }

    private async Task<string> RenderAsync() =>
        (await scenario.Client.GetAsync("/Default.aspx")).Text;

    private Task<ScenarioResponse> ApplyAsync(string html) =>
        PostAsync(
            html,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Message"] = Message,
                ["Apply"] = "Apply",
            });

    private Task<ScenarioResponse> PostAsync(string html, IDictionary<string, string> overrides) =>
        scenario.Client.PostFormAsync(
            PostbackForm.Action(html),
            PostbackForm.Body(html, overrides));

    private static string FlipOneCharacter(string value)
    {
        var characters = value.ToCharArray();
        var middle = characters.Length / 2;
        characters[middle] = characters[middle] == 'A' ? 'B' : 'A';

        return new string(characters);
    }
}
