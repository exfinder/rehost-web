<%@ Application Language="C#" %>
<%@ Import Namespace="Microsoft.AspNet.FriendlyUrls" %>
<%@ Import Namespace="System.Web.Routing" %>
<script runat="server">

    void Application_Start(object sender, EventArgs e)
    {
        RouteTable.Routes.EnableFriendlyUrls(new FriendlyUrlSettings
        {
            AutoRedirectMode = RedirectMode.Permanent
        });
    }

</script>
