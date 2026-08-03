<%@ Page Language="C#" %>
<script runat="server">

    // Assigned only on the initial render, so its value after a postback can only have been
    // restored from view state — including view state another runtime serialized.
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            Carried.Text = "carried-from-initial";
        }
    }

    protected void Apply_Click(object sender, EventArgs e)
    {
        Echo.Text = "clicked";
    }

</script>
<html>
<head><title>Postback fixture</title></head>
<body>
<form id="postback" runat="server">
<asp:TextBox ID="Message" runat="server" />
<asp:Button ID="Apply" runat="server" Text="Apply" OnClick="Apply_Click" />
<asp:Label ID="Carried" runat="server" />
<asp:Label ID="Echo" runat="server" />
<p id="state">postback=<%= IsPostBack %>|carried=<%= Carried.Text %>|echo=<%= Echo.Text %></p>
</form>
</body>
</html>
