<%@ Page Language="C#" MasterPageFile="~/Site.master" %>
<%@ Register TagPrefix="s" Namespace="Sample.Controls" %>
<script runat="server">

    private int _perRequest;

    protected void Page_Load(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, IsPostBack ? "Page_Load (postback)" : "Page_Load (initial)");
        _perRequest++;

        if (!IsPostBack)
        {
            ViewStateCount.Text = "0";
        }
    }

    protected void Bump_Click(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "Bump_Click");
        ViewStateCount.Text = (int.Parse(ViewStateCount.Text) + 1).ToString();
        StateCounter.Increment();
        Echo.Text = Server.HtmlEncode(Message.Text);
    }

    protected void BumpLink_Click(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "BumpLink_Click");
        Bump_Click(sender, e);
    }

    protected void Message_TextChanged(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "Message_TextChanged");
    }

    protected override void OnPreRender(EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "OnPreRender");
        PerRequestCount.Text = _perRequest.ToString();
        base.OnPreRender(e);
    }

</script>
<asp:Content ContentPlaceHolderID="Main" runat="server">

<h1>Postback &amp; state</h1>
<p class="lede">
    Three counters, three lifetimes. Click either button and watch which ones survive the
    round trip — the pipeline trace in the footer shows the events as they fired.
</p>

<div class="panel">
    <p>
        <asp:TextBox ID="Message" runat="server" OnTextChanged="Message_TextChanged" />
        <asp:Button ID="Bump" runat="server" Text="Post back (Button)" OnClick="Bump_Click" />
        <asp:LinkButton ID="BumpLink" runat="server" Text="post back via LinkButton" OnClick="BumpLink_Click" />
    </p>
    <p>Last message through the form: <strong><asp:Label ID="Echo" runat="server" Text="(none)" /></strong></p>
</div>

<div class="cards">
    <div class="card stat">
        <h2><asp:Label ID="ViewStateCount" runat="server" /></h2>
        <p><strong>View state.</strong> A Label's Text, carried in <code>__VIEWSTATE</code>,
        MAC-protected. Survives every postback.</p>
    </div>
    <div class="card stat">
        <h2><s:ClickCounter ID="StateCounter" runat="server" EnableViewState="false" /></h2>
        <p><strong>Control state.</strong> An <code>App_Code</code> control with
        <code>EnableViewState="false"</code> — view state is off, control state still rides.</p>
    </div>
    <div class="card stat">
        <h2><asp:Label ID="PerRequestCount" runat="server" /></h2>
        <p><strong>Per-request field.</strong> A page instance field. A new page object is built
        every request, so it never accumulates.</p>
    </div>
</div>

</asp:Content>
