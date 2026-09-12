<%@ Page Language="C#" %>
<!DOCTYPE html>
<html>
<head runat="server"><title>rewrite probe</title></head>
<body>
<form id="rw" runat="server">
path=<%= Request.Path %>
rawurl=<%= Request.RawUrl %>
query=<%= Request.QueryString["id"] %>
xoriginal=<%= Request.Headers["X-Original-URL"] %>
rewritten=<%= Request.ServerVariables["IIS_WasUrlRewritten"] %>
inallkeys=<%= Array.IndexOf(Request.ServerVariables.AllKeys, "IIS_WasUrlRewritten") >= 0 %>
</form>
</body>
</html>
