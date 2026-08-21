<%@ Page Language="C#" %>
<script runat="server">

    [System.Web.Services.WebMethod]
    public static string Echo(string text)
    {
        return "pm:" + text;
    }

</script>
<!DOCTYPE html>
<html>
<head runat="server"><title>Methods</title></head>
<body>
    <form id="Form1" runat="server">
        <asp:ScriptManager ID="SM" runat="server" EnablePageMethods="true" />
    </form>
</body>
</html>
