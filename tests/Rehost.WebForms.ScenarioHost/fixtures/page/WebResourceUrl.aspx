<%@ Page Language="C#" %>
<!DOCTYPE html>
<html>
<head runat="server"><title>web resource url</title></head>
<body>
    <form id="form1" runat="server">
        <span id="url"><%= ClientScript.GetWebResourceUrl(typeof(System.Web.UI.Page), "Spacer.gif") %></span>
    </form>
</body>
</html>
