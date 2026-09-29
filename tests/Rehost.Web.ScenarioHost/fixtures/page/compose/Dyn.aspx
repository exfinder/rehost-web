<%@ Page Language="C#" MasterPageFile="~/compose/Site.master" Title="LoadControl probe" %>
<script runat="server">
    protected void Page_Init(object sender, EventArgs e) {
        var widget = LoadControl("~/compose/Widget.ascx");
        widget.ID = "DynWidget";
        Slot.Controls.Add(widget);
    }
</script>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:PlaceHolder ID="Slot" runat="server" />
</asp:Content>
