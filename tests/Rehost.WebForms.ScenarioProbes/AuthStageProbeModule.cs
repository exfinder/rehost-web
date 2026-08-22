using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

public sealed class AuthStageProbeModule : IHttpModule
{
    internal const string ItemKey = "auth-stage";

    public void Init(HttpApplication application)
    {
        application.PostAuthenticateRequest += (sender, _) =>
        {
            var context = ((HttpApplication)sender!).Context;
            context.Items[ItemKey] = string.Join(
                "/",
                context.User?.GetType().Name ?? "<null>",
                context.User?.Identity?.IsAuthenticated.ToString() ?? "<null>",
                context.Request.Cookies[".ASPXROLES"] is null ? "nocookie" : "cookie",
                context.User is System.Web.Security.RolePrincipal role ? "cached=" + role.IsRoleListCached : "norole",
                "fetched=" + FakeRoleProvider.FetchesFor(context.User?.Identity?.Name ?? string.Empty));
        };
    }

    public void Dispose()
    {
    }
}
