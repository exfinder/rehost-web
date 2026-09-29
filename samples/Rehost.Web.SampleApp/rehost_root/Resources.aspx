<%@ Page Language="C#" MasterPageFile="~/Site.master" %>
<script runat="server">

    protected override void InitializeCulture()
    {
        var culture = Request.QueryString["culture"];
        if (!string.IsNullOrEmpty(culture))
        {
            var info = System.Globalization.CultureInfo.GetCultureInfo(culture);
            System.Threading.Thread.CurrentThread.CurrentUICulture = info;
            System.Threading.Thread.CurrentThread.CurrentCulture = info;
        }
        base.InitializeCulture();
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "Resources.Page_Load");
        Greeting.Text = Resources.Strings.Greeting;
        Tagline.Text = Resources.Strings.Tagline;
        Culture_.Text = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
    }

</script>
<asp:Content ContentPlaceHolderID="Main" runat="server">

<h1>Global resources</h1>
<p class="lede">
    <code>App_GlobalResources</code> compiles to a strongly typed class — the strings below are
    <code>Resources.Strings.Greeting</code> and <code>.Tagline</code>, resolved against the
    current UI culture with the neutral <code>.resx</code> as fallback.
</p>

<div class="panel">
    <h2><asp:Label ID="Greeting" runat="server" /></h2>
    <p><asp:Label ID="Tagline" runat="server" /></p>
    <p>Current UI culture: <strong><asp:Label ID="Culture_" runat="server" /></strong></p>
    <p>
        <a href="Resources.aspx">neutral</a> ·
        <a href="Resources.aspx?culture=fr-FR">?culture=fr-FR</a> ·
        <a href="Resources.aspx?culture=de-DE">?culture=de-DE (no satellite — falls back)</a>
    </p>
</div>

</asp:Content>
