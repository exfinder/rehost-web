<%@ Page Language="C#" %>
<%@ Import Namespace="Microsoft.AspNet.FriendlyUrls" %>
<script runat="server">

    protected override void OnLoad(EventArgs e)
    {
        Marker.Text = Request.GetFriendlyUrlFileVirtualPath() + "|" + Request.RawUrl + "|" + Request.Path;
    }

</script>
<html><body><form runat="server"><asp:Literal runat="server" ID="Marker" /></form></body></html>
