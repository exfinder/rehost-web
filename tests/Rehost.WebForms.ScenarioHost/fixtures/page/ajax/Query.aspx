<%@ Page Language="C#" %>
<%@ Register TagPrefix="fx" Namespace="Rehost.Fixtures.AjaxQuery" %>
<!DOCTYPE html>
<html>
<head runat="server"><title>Query</title></head>
<body>
    <form id="Form1" runat="server">
        <fx:AjaxQueryPeopleDataSource ID="People" runat="server" />
        <asp:QueryExtender ID="QE" runat="server" TargetControlID="People">
            <asp:RangeExpression DataField="Age" MinType="Inclusive" MaxType="Inclusive">
                <asp:Parameter DefaultValue="30" Type="Int32" />
                <asp:Parameter DefaultValue="90" Type="Int32" />
            </asp:RangeExpression>
            <asp:OrderByExpression DataField="Age" Direction="Descending" />
        </asp:QueryExtender>
        <asp:Repeater ID="List" runat="server" DataSourceID="People">
            <ItemTemplate>[<%# Eval("Name") %>:<%# Eval("Age") %>]</ItemTemplate>
        </asp:Repeater>
    </form>
</body>
</html>
