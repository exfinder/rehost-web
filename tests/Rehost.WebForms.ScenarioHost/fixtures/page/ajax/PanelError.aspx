<%@ Page Language="C#" %>
<script runat="server">

    protected void Fail_Click(object sender, EventArgs e)
    {
        throw new ApplicationException("panel-deliberate-failure");
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
<head runat="server"><title>PanelError</title></head>
<body>
    <form id="Form1" runat="server">
        <asp:ScriptManager ID="SM" runat="server" OnAsyncPostBackError="SM_AsyncPostBackError" />
        <asp:UpdatePanel ID="Panel" runat="server">
            <ContentTemplate>
                <asp:Button ID="Fail" runat="server" Text="Fail" OnClick="Fail_Click" />
            </ContentTemplate>
        </asp:UpdatePanel>
    </form>
</body>
</html>
