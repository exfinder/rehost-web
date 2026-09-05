<%@ Application Language="C#" %>
<%@ Import Namespace="Microsoft.AspNet.FriendlyUrls" %>
<%@ Import Namespace="Microsoft.AspNet.FriendlyUrls.Resolvers" %>
<%@ Import Namespace="System.Web.Routing" %>
<script runat="server">

    void Application_Start(object sender, EventArgs e)
    {
        RouteTable.Routes.MapOwinPath("/owin-ws");
        RouteTable.Routes.EnableFriendlyUrls(
            new FriendlyUrlSettings
            {
                AutoRedirectMode = RedirectMode.Permanent
            },
            new WebFormsFriendlyUrlResolver(),
            new GenericHandlerFriendlyUrlResolver());
    }

</script>
