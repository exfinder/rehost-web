<%@ Page Language="C#" MasterPageFile="~/compose/Site.master" Title="Master form probe" %>
<script runat="server">
    protected void Submit_Click(object sender, EventArgs e) {
        Echo.Text = "clicked-with:" + Entry.Text;
    }
</script>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:TextBox ID="Entry" runat="server" />
    <asp:Button ID="Submit" runat="server" Text="Go" OnClick="Submit_Click" />
    <p id="echo"><asp:Label ID="Echo" runat="server" Text="not-clicked" /></p>
</asp:Content>
