<%@ Page Language="C#" %>
<script runat="server">

    [System.Web.Services.WebMethod]
    public static string Echo(string text)
    {
        return "pm:" + text;
    }

    protected void Bump_Click(object sender, EventArgs e)
    {
        Inside.Text = "inside:bumped";
        Outside.Text = "outside:bumped";
    }

    protected void Refresh_Click(object sender, EventArgs e)
    {
        Inside.Text = "inside:refreshed";
    }

    protected void Fail_Click(object sender, EventArgs e)
    {
        throw new ApplicationException("panel-deliberate-failure");
    }

    protected void Go_Click(object sender, EventArgs e)
    {
        Response.Redirect("~/ajax/Panel.aspx?from=redirect");
    }

    protected void T_Tick(object sender, EventArgs e)
    {
        Clock.Text = "clock:ticked";
    }

    protected void SM_AsyncPostBackError(object sender, System.Web.UI.AsyncPostBackErrorEventArgs e)
    {
        if (Request.Form["Friendly"] == "yes")
        {
            SM.AsyncPostBackErrorMessage = "panel-friendly-message";
        }
    }

</script>
<!DOCTYPE html>
<html>
<head runat="server"><title>Panel</title></head>
<body>
    <form id="Form1" runat="server">
        <asp:ScriptManager ID="SM" runat="server" EnablePageMethods="true"
            OnAsyncPostBackError="SM_AsyncPostBackError" />
        <asp:Timer ID="T" runat="server" Interval="60000" OnTick="T_Tick" />
        <asp:Label ID="Outside" runat="server" Text="outside:initial" />
        <asp:UpdatePanel ID="Panel" runat="server" UpdateMode="Conditional" ChildrenAsTriggers="true">
            <ContentTemplate>
                <asp:Label ID="Inside" runat="server" Text="inside:initial" />
                <asp:Label ID="Clock" runat="server" Text="clock:initial" />
                <asp:Button ID="Bump" runat="server" Text="Bump" OnClick="Bump_Click" />
                <asp:Button ID="Fail" runat="server" Text="Fail" OnClick="Fail_Click" />
                <asp:Button ID="Go" runat="server" Text="Go" OnClick="Go_Click" />
            </ContentTemplate>
            <Triggers>
                <asp:AsyncPostBackTrigger ControlID="Refresh" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="T" EventName="Tick" />
            </Triggers>
        </asp:UpdatePanel>
        <asp:Button ID="Refresh" runat="server" Text="Refresh" OnClick="Refresh_Click" />
    </form>
</body>
</html>
