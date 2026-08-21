<%@ Page Language="C#" %>
<script runat="server">

    protected void Go_Click(object sender, EventArgs e)
    {
        Response.Redirect("~/ajax/Panel.aspx?from=redirect");
    }

</script>
<!DOCTYPE html>
<html>
<head runat="server"><title>PanelRedirect</title></head>
<body>
    <form id="Form1" runat="server">
        <asp:ScriptManager ID="SM" runat="server" />
        <asp:UpdatePanel ID="Panel" runat="server">
            <ContentTemplate>
                <asp:Button ID="Go" runat="server" Text="Go" OnClick="Go_Click" />
            </ContentTemplate>
        </asp:UpdatePanel>
    </form>
</body>
</html>
