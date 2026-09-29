<%@ Page Language="C#" %>
<!DOCTYPE html>
<html>
<head runat="server"><title>rewrite probe</title></head>
<body>
<form id="rw" runat="server">
path=<%= Request.Path %>
pathinfo=<%= Request.PathInfo %>
rawurl=<%= Request.RawUrl %>
query=<%= Request.QueryString %>
xoriginal=<%= Request.Headers["X-Original-URL"] %>
rewritten=<%= Request.ServerVariables["IIS_WasUrlRewritten"] %>
requesturi=<%= Request.ServerVariables["REQUEST_URI"] %>
inallkeys=<%= Array.IndexOf(Request.ServerVariables.AllKeys, "IIS_WasUrlRewritten") >= 0 %>,<%= Array.IndexOf(Request.ServerVariables.AllKeys, "REQUEST_URI") >= 0 %>,<%= Array.IndexOf(Request.ServerVariables.AllKeys, "UNENCODED_URL") >= 0 %>,<%= Array.IndexOf(Request.ServerVariables.AllKeys, "CACHE_URL") >= 0 %>
</form>
</body>
</html>
