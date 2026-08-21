<%@ Page Language="C#" %>
<script runat="server">

    protected void Bump_Click(object sender, EventArgs e)
    {
        Inside.Text = "inside:bumped";
        Outside.Text = "outside:bumped";
    }

    protected void Refresh_Click(object sender, EventArgs e)
    {
        Inside.Text = "inside:refreshed";
    }

</script>
<!DOCTYPE html>
<html>
<head runat="server"><title>Panel</title></head>
<body>
    <form id="Form1" runat="server">
        <asp:ScriptManager ID="SM" runat="server" />
        <asp:Label ID="Outside" runat="server" Text="outside:initial" />
        <asp:UpdatePanel ID="Panel" runat="server" UpdateMode="Conditional" ChildrenAsTriggers="true">
            <ContentTemplate>
                <asp:Label ID="Inside" runat="server" Text="inside:initial" />
                <asp:Button ID="Bump" runat="server" Text="Bump" OnClick="Bump_Click" />
            </ContentTemplate>
            <Triggers>
                <asp:AsyncPostBackTrigger ControlID="Refresh" EventName="Click" />
            </Triggers>
        </asp:UpdatePanel>
        <asp:Button ID="Refresh" runat="server" Text="Refresh" OnClick="Refresh_Click" />
    </form>
</body>
</html>
