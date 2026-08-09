<%@ Page Language="C#" %>
<%@ Import Namespace="Microsoft.AspNet.FriendlyUrls" %>
<script runat="server">

    protected override void OnLoad(EventArgs e)
    {
        Response.ContentType = "text/plain";
        Response.Write(FriendlyUrl.Resolve("~/About.aspx"));
        Response.Write("|");
        Response.Write(FriendlyUrl.Href("~/About", "one", 3));
        Response.Write("|");
        Response.Write(string.Join("/", FriendlyUrl.Segments));
    }

</script>
