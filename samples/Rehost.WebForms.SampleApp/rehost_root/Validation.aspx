<%@ Page Language="C#" MasterPageFile="~/Site.master" %>
<script runat="server">

    protected void Page_Load(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "Validation.Page_Load");

        var q = Request.QueryString["q"];
        if (q != null)
        {
            EchoQuery.Text = "The query value <code>" + Server.HtmlEncode(q)
                + "</code> passed validation and reached the page.";
        }
    }

    protected void Send_Click(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "Send_Click");
        EchoForm.Text = "The form value <code>" + Server.HtmlEncode(Attempt.Text)
            + "</code> passed validation and reached the page.";
    }

</script>
<asp:Content ContentPlaceHolderID="Main" runat="server">

<h1>Request validation</h1>
<p class="lede">
    Framework's default request validation runs before the page does: a query string or form
    value that looks like markup is refused with the classic
    <em>"A potentially dangerous Request value was detected"</em> error page. Try to get
    <code>&lt;script&gt;</code> through.
</p>

<div class="panel">
    <h2>Via the form</h2>
    <p>
        <asp:TextBox ID="Attempt" runat="server" Text="&lt;script&gt;alert(1)&lt;/script&gt;" />
        <asp:Button ID="Send" runat="server" Text="Post it" OnClick="Send_Click" />
    </p>
    <p><asp:Label ID="EchoForm" runat="server" /></p>
</div>

<div class="panel">
    <h2>Via the query string</h2>
    <p>
        <a href="Validation.aspx?q=%3Cscript%3Ealert(1)%3C%2Fscript%3E">?q=&lt;script&gt;alert(1)&lt;/script&gt;</a>
        — refused before Page_Load.
    </p>
    <p>
        <a href="Validation.aspx?q=perfectly%20harmless">?q=perfectly harmless</a>
        — passes, and the page echoes it (HTML-encoded) below.
    </p>
    <p><asp:Label ID="EchoQuery" runat="server" /></p>
</div>

</asp:Content>
