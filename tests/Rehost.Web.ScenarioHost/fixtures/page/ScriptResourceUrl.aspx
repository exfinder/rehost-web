<%@ Page Language="C#" %>
<!DOCTYPE html>
<html>
<head runat="server"><title>script resource url</title></head>
<body>
    <form id="form1" runat="server">
        <asp:ScriptManager ID="Scripts" runat="server" EnableCdn="false">
            <Scripts>
                <asp:ScriptReference Name="MicrosoftAjaxCore.js" Path="" />
            </Scripts>
        </asp:ScriptManager>
    </form>
</body>
</html>
