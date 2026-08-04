<%@ Page Language="C#" %>
<%@ Register TagPrefix="p" Namespace="Rehost.WebForms.ScenarioProbes" Assembly="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    // Carried is written only here, so on a postback its value can only come from view state.
    protected void Page_Load(object sender, EventArgs e)
    {
        PostbackTrace.Record(Context, IsPostBack ? "page.load.postback" : "page.load.initial");

        if (!IsPostBack)
        {
            Carried.Text = "carried-from-initial";
            Ticker.Note = "note-from-initial";
        }
    }

    protected void Message_TextChanged(object sender, EventArgs e)
    {
        PostbackTrace.Record(Context, "message.text-changed");
    }

    protected void Apply_Click(object sender, EventArgs e)
    {
        PostbackTrace.Record(Context, "apply.click");
        Ticker.Increment();
        Echo.Text = "applied:" + Message.Text;
    }

    protected void Bump_Click(object sender, EventArgs e)
    {
        PostbackTrace.Record(Context, "bump.click");
        Ticker.Increment();
        Echo.Text = "bumped";
    }

    protected override void OnInit(EventArgs e)
    {
        PostbackTrace.Record(Context, "page.init");
        base.OnInit(e);
    }

    protected override void OnPreRender(EventArgs e)
    {
        PostbackTrace.Record(Context, "page.prerender");
        base.OnPreRender(e);
    }

    protected string Restored
    {
        get
        {
            // A posted __VIEWSTATE that left IsPostBack false happens only when a MAC failure
            // was suppressed: a valid one makes it true, an unsuppressed invalid one throws.
            return "postback=" + IsPostBack
                + "|posted-viewstate=" + (Request.Form["__VIEWSTATE"] != null)
                + "|clicks=" + Ticker.Clicks
                + "|note=" + Ticker.Note
                + "|carried=" + Carried.Text
                + "|message=" + Message.Text
                + "|form=" + Request.Form["Message"]
                + "|echo=" + Echo.Text;
        }
    }

</script>
<html>
<head><title>Postback fixture</title></head>
<body>
<form id="postback" runat="server">
<asp:TextBox ID="Message" runat="server" OnTextChanged="Message_TextChanged" />
<asp:Button ID="Apply" runat="server" Text="Apply" OnClick="Apply_Click" />
<asp:LinkButton ID="Bump" runat="server" Text="Bump" OnClick="Bump_Click" />
<p:Counter ID="Ticker" runat="server" EnableViewState="false" />
<asp:Label ID="Carried" runat="server" />
<asp:Label ID="Echo" runat="server" />
<p id="restored"><%= Restored %></p>
<p id="trace"><%= PostbackTrace.Render(Context) %></p>
</form>
</body>
</html>
