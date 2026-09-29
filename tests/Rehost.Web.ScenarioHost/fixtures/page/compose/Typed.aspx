<%@ Page Language="C#" MasterPageFile="~/compose/Site.master" Title="Typed master probe" %>
<%@ MasterType VirtualPath="~/compose/Site.master" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        Master.ChromeText = "set-through-typed-master";
    }
</script>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <p id="main">typed-master-page</p>
</asp:Content>
