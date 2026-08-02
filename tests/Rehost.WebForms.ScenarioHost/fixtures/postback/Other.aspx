<%@ Page Language="C#" %>
<script runat="server">

    protected void Page_Load(object sender, EventArgs e)
    {
        Marker.Text = "other-page";
    }

</script>
<html>
<head><title>Other fixture</title></head>
<body>
<form id="other" runat="server">
<asp:Label ID="Marker" runat="server" />
</form>
</body>
</html>
