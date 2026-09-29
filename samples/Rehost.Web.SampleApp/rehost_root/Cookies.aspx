<%@ Page Language="C#" MasterPageFile="~/Site.master" %>
<script runat="server">

    protected void Page_Load(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "Cookies.Page_Load");
        if (!IsPostBack)
        {
            BindCookies();
        }
    }

    protected void SetCookie_Click(object sender, EventArgs e)
    {
        var name = CookieName.Text.Trim();
        if (name.Length == 0)
        {
            Status.Text = "A cookie needs a name.";
            BindCookies();
            return;
        }

        var cookie = new HttpCookie(name, CookieValue.Text);
        if (Persistent.Checked)
        {
            cookie.Expires = DateTime.Now.AddMinutes(10);
        }
        Response.Cookies.Add(cookie);

        Status.Text = "Set-Cookie sent for '" + Server.HtmlEncode(name)
            + "'" + (Persistent.Checked ? " (expires in 10 minutes)" : " (session)") + ".";
        BindCookies();
    }

    protected void DeleteCookie_Click(object sender, EventArgs e)
    {
        var name = CookieName.Text.Trim();
        var expired = new HttpCookie(name) { Expires = DateTime.Now.AddDays(-1) };
        Response.Cookies.Add(expired);
        Status.Text = "Sent an expired Set-Cookie for '" + Server.HtmlEncode(name)
            + "' — deleting a browser cookie is done by expiring it.";
        BindCookies();
    }

    private void BindCookies()
    {
        var rows = new System.Collections.Generic.List<string>();
        foreach (string name in Request.Cookies)
        {
            rows.Add(name + " = " + Request.Cookies[name].Value);
        }
        Current.DataSource = rows;
        Current.DataBind();
        None.Visible = rows.Count == 0;
    }

</script>
<asp:Content ContentPlaceHolderID="Main" runat="server">

<h1>Cookies</h1>
<p class="lede">
    <code>Response.Cookies</code> emits one <code>Set-Cookie</code> line per cookie with
    Framework's exact attribute text; a response cookie is immediately visible in the same
    request's <code>Request.Cookies</code>, which is why a cookie you set appears in the list
    below before the browser ever echoes it back.
</p>

<div class="panel">
    <p>
        <asp:TextBox ID="CookieName" runat="server" Text="flavor" />
        <asp:TextBox ID="CookieValue" runat="server" Text="oatmeal" />
        <asp:CheckBox ID="Persistent" runat="server" Text="persistent (10 min)" />
    </p>
    <p>
        <asp:Button ID="SetCookie" runat="server" Text="Set cookie" OnClick="SetCookie_Click" />
        <asp:Button ID="DeleteCookie" runat="server" Text="Delete cookie" OnClick="DeleteCookie_Click" />
    </p>
    <p><asp:Label ID="Status" runat="server" /></p>
</div>

<div class="panel">
    <h2>Cookies on this request</h2>
    <asp:Label ID="None" runat="server" Text="(none)" />
    <asp:Repeater ID="Current" runat="server">
        <ItemTemplate><span class="chip"><%# Server.HtmlEncode((string)Container.DataItem) %></span></ItemTemplate>
    </asp:Repeater>
</div>

</asp:Content>
