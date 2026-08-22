using System.Web;
using System.Web.Security;

namespace Rehost.WebForms.ScenarioProbes;

public sealed class AuthProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var request = context.Request;
        var user = context.User;

        var report = string.Join(
            ";",
            "auth:" + request.IsAuthenticated,
            "name:" + (user?.Identity is null ? "<nouser>" : user.Identity.Name),
            "identity:" + (user?.Identity is null ? "<nouser>" : user.Identity.GetType().Name),
            "principal:" + (user is null ? "<nouser>" : user.GetType().Name),
            "editors:" + (user is null ? "<nouser>" : user.IsInRole(FakeRoleProvider.KnownRole).ToString()),
            "anonid:" + (request.AnonymousID is null ? "<null>" : "set"),
            "profileuser:" + (context.Profile is null ? "<null>" : Describe(context.Profile.UserName, request.AnonymousID)),
            "stage:" + (context.Items[AuthStageProbeModule.ItemKey] ?? "<none>"),
            "fetches:" + FakeRoleProvider.FetchesFor(user?.Identity?.Name ?? string.Empty),
            "nickname:[" + (context.Profile?.GetPropertyValue("Nickname") ?? string.Empty) + "]");

        RequestBodyHandler.WriteResult(context, report);
    }

    private static string Describe(string profileUser, string? anonymousId)
        => profileUser == anonymousId ? "<anonymousid>" : profileUser;
}

public sealed class SignInProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var name = context.Request.QueryString["u"] ?? string.Empty;
        var password = context.Request.QueryString["p"] ?? string.Empty;
        var validated = Membership.ValidateUser(name, password);

        if (validated)
        {
            FormsAuthentication.SetAuthCookie(name, false);
        }

        RequestBodyHandler.WriteResult(context, "validate:" + validated);
    }
}

public sealed class ProfileWriteProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var profile = context.Profile;
        profile.SetPropertyValue("Nickname", context.Request.QueryString["n"] ?? string.Empty);
        profile.Save();

        RequestBodyHandler.WriteResult(context, "saved:" + profile.GetPropertyValue("Nickname"));
    }
}

public sealed class ProtectedProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
        => RequestBodyHandler.WriteResult(context, "secret-ok:" + context.User?.Identity?.Name);
}
