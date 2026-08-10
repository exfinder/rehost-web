<%@ Page Language="C#" %>
<!DOCTYPE html>
<html>
<head runat="server"><title>design-time controls</title></head>
<body>
    <form id="form1" runat="server">
        <asp:Localize ID="Loc" runat="server" Text="localize-rendered" />
        <asp:SqlDataSource ID="Sql" runat="server" />
        <asp:ObjectDataSource ID="Obj" runat="server" />
        <asp:XmlDataSource ID="Xml" runat="server" />
        <asp:AccessDataSource ID="Access" runat="server" />
        <asp:SiteMapDataSource ID="Map" runat="server" />
    </form>
</body>
</html>
