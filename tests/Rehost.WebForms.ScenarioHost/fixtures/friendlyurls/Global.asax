<%@ Application Language="C#" %>
<%@ Import Namespace="Microsoft.AspNet.FriendlyUrls" %>
<%@ Import Namespace="Microsoft.AspNet.FriendlyUrls.Resolvers" %>
<%@ Import Namespace="System.Reflection" %>
<%@ Import Namespace="System.Web.Http" %>
<%@ Import Namespace="System.Web.Http.WebHost" %>
<%@ Import Namespace="System.Web.Routing" %>
<script runat="server">

    void Application_Start(object sender, EventArgs e)
    {
        FieldInfo instance = typeof(HttpControllerRouteHandler).GetField(
            "_instance", BindingFlags.Static | BindingFlags.NonPublic);
        instance.SetValue(
            null,
            new Lazy<HttpControllerRouteHandler>(() => new SessionHttpControllerRouteHandler(), true));
        GlobalConfiguration.Configure(WebApiConfig.Register);

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
