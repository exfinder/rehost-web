<%@ Page Language="C#" MasterPageFile="~/compose/Site.master" Title="From content page" %>
<%@ Register Src="~/compose/Widget.ascx" TagPrefix="uc" TagName="Widget" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        PageLabel.Text = "content-load-ran";
    }
</script>
<asp:Content ContentPlaceHolderID="HeadContent" runat="server">
    <meta name="probe" content="head-content" />
</asp:Content>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <p id="main"><asp:Label ID="PageLabel" runat="server" /></p>
    <uc:Widget ID="TheWidget" runat="server" />
</asp:Content>
