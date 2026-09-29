<%@ Page Language="C#" %>
<!DOCTYPE html>
<html>
<head><title>configured provider</title></head>
<body>
    <asp:SqlDataSource
        ID="Rows"
        runat="server"
        ProviderName="Rehost.ScenarioProbes.FixedRows"
        ConnectionString="fixed"
        SelectCommand="rows"
        DataSourceMode="DataReader" />
    <asp:Repeater ID="List" runat="server" DataSourceID="Rows">
        <ItemTemplate>[<%# Eval("Name") %>]</ItemTemplate>
    </asp:Repeater>
</body>
</html>
