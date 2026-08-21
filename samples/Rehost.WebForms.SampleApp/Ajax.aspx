<%@ Page Language="C#" MasterPageFile="~/Site.master" %>
<script runat="server">

    protected void Page_Load(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, IsPostBack ? "Page_Load (postback)" : "Page_Load (initial)");

        if (!IsPostBack)
        {
            PanelStamp.Text = DateTime.Now.ToString("HH:mm:ss.fff");
            PageStamp.Text = PanelStamp.Text;
            Clicks.Text = "0";
        }
    }

    protected void Bump_Click(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "Bump_Click (async)");
        Clicks.Text = (int.Parse(Clicks.Text) + 1).ToString();
        PanelStamp.Text = DateTime.Now.ToString("HH:mm:ss.fff");
        Echo.Text = Server.HtmlEncode(Message.Text);
    }

    protected void T_Tick(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "Timer tick (async)");
        PanelStamp.Text = DateTime.Now.ToString("HH:mm:ss.fff");
    }

</script>
<asp:Content ContentPlaceHolderID="Main" runat="server">

<h1>UpdatePanel &amp; async postbacks</h1>
<p class="lede">
    The panel below refreshes over an async postback — a five-second timer and a button both
    update it — while the timestamp outside the panel stays frozen at the full page load.
</p>

<div class="panel">
    <p>Rendered with the full page: <strong><asp:Label ID="PageStamp" runat="server" /></strong></p>
    <asp:ScriptManager ID="SM" runat="server" />
    <asp:Timer ID="T" runat="server" Interval="5000" OnTick="T_Tick" />
    <asp:UpdatePanel ID="Panel" runat="server">
        <ContentTemplate>
            <p>
                Rendered by the panel: <strong><asp:Label ID="PanelStamp" runat="server" /></strong>
                after <strong><asp:Label ID="Clicks" runat="server" /></strong> click(s).
            </p>
            <p>
                <asp:TextBox ID="Message" runat="server" />
                <asp:Button ID="Bump" runat="server" Text="Post back inside the panel" OnClick="Bump_Click" />
            </p>
            <p>Last message through the panel: <strong><asp:Label ID="Echo" runat="server" Text="(none)" /></strong></p>
        </ContentTemplate>
        <Triggers>
            <asp:AsyncPostBackTrigger ControlID="T" EventName="Tick" />
        </Triggers>
    </asp:UpdatePanel>
    <asp:UpdateProgress ID="Progress" runat="server" AssociatedUpdatePanelID="Panel">
        <ProgressTemplate><em>updating…</em></ProgressTemplate>
    </asp:UpdateProgress>
</div>

</asp:Content>
