<%@ Page Language="C#" %>
<%@ Import Namespace="Microsoft.AspNet.FriendlyUrls" %>
<script runat="server">

    protected override void OnLoad(EventArgs e)
    {
        Response.ContentType = "text/plain";
        Response.Write(Request.GetFriendlyUrlFileVirtualPath());
        Response.Write("|");
        Response.Write(string.Join("|", Request.GetFriendlyUrlSegments()));
    }

</script>
