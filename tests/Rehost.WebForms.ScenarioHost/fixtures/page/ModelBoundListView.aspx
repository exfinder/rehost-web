<%@ Page Language="C#" %>
<script runat="server">
    public IEnumerable<string> GetNames()
    {
        return new[] { "alpha", "beta" };
    }
</script>
<!DOCTYPE html>
<html>
<head runat="server"><title>model-bound list view</title></head>
<body>
    <form id="form1" runat="server">
        <asp:ListView ID="Names" runat="server" ItemType="System.String" SelectMethod="GetNames">
            <ItemTemplate><span class="name"><%#: Item %></span></ItemTemplate>
            <EmptyDataTemplate><span class="empty">no names</span></EmptyDataTemplate>
        </asp:ListView>
    </form>
</body>
</html>
