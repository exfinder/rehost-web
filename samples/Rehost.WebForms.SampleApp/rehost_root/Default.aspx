<%@ Page Language="C#" MasterPageFile="~/Site.master" %>
<%@ Import Namespace="Sample" %>
<script runat="server">

    protected void Page_Load(object sender, EventArgs e)
    {
        SampleTrace.Record(Context, "Default.Page_Load");
        Features.DataSource = FeatureCatalog.Pages;
        Features.DataBind();

        var names = new System.Collections.Generic.List<string>();
        foreach (string name in Request.Cookies)
        {
            names.Add(name + " = " + Request.Cookies[name].Value);
        }
        CookieList.DataSource = names;
        CookieList.DataBind();
        NoCookies.Visible = names.Count == 0;
    }

</script>
<asp:Content ContentPlaceHolderID="Main" runat="server">

<h1>Classic Web Forms, rehosted</h1>
<p class="lede">
    Every page here is a real <code>.aspx</code>, parsed and compiled at first request by the
    rehosted <code>System.Web</code> pipeline, served over Kestrel. No IIS, no Windows, no
    pre-conversion — the chrome around this text is a <code>Site.master</code> and this list is
    an <code>&lt;asp:Repeater&gt;</code>.
</p>

<div class="cards">
<asp:Repeater ID="Features" runat="server">
    <ItemTemplate>
        <a class="card" href='<%# ResolveUrl((string)DataBinder.Eval(Container.DataItem, "Url")) %>'>
            <h2><%# DataBinder.Eval(Container.DataItem, "Title") %></h2>
            <p><%# DataBinder.Eval(Container.DataItem, "Blurb") %></p>
        </a>
    </ItemTemplate>
</asp:Repeater>
</div>

<asp:Panel ID="RequestInfo" runat="server" CssClass="panel">
    <h2>This request, as System.Web saw it</h2>
    <dl>
        <dt>Method / path</dt><dd><%= Request.HttpMethod %> <%= Request.Path %></dd>
        <dt>User agent</dt><dd><%= Server.HtmlEncode(Request.UserAgent ?? "(none)") %></dd>
        <dt>Physical path</dt><dd><%= Request.PhysicalPath %></dd>
        <dt>Cookies</dt>
        <dd>
            <asp:Label ID="NoCookies" runat="server" Text="(none yet — visit the Cookies page)" />
            <asp:Repeater ID="CookieList" runat="server">
                <ItemTemplate><span class="chip"><%# Server.HtmlEncode((string)Container.DataItem) %></span></ItemTemplate>
            </asp:Repeater>
        </dd>
    </dl>
</asp:Panel>

</asp:Content>
