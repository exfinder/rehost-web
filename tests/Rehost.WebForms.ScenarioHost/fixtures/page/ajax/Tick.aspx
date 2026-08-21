<%@ Page Language="C#" %>
<script runat="server">

    protected void T_Tick(object sender, EventArgs e)
    {
        Clock.Text = "clock:ticked";
    }

</script>
<!DOCTYPE html>
<html>
<head runat="server"><title>Tick</title></head>
<body>
    <form id="Form1" runat="server">
        <asp:ScriptManager ID="SM" runat="server" />
        <asp:Timer ID="T" runat="server" Interval="60000" OnTick="T_Tick" />
        <asp:UpdatePanel ID="Panel" runat="server">
            <ContentTemplate>
                <asp:Label ID="Clock" runat="server" Text="clock:initial" />
            </ContentTemplate>
            <Triggers>
                <asp:AsyncPostBackTrigger ControlID="T" EventName="Tick" />
            </Triggers>
        </asp:UpdatePanel>
    </form>
</body>
</html>
