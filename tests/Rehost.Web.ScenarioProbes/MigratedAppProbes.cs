using System.Web;

namespace Rehost.Web.ScenarioProbes;

// The webServer-only registrations a surveyed production configuration carries: an auth gate that
// also covers static assets, an async replacement swapped in under the inherited Session name, and
// a response-header amendment.
public sealed class SessionSwapModule() : ModuleEventProbe("session-swap")
{
    private protected override void Extend(HttpApplication application)
    {
        var helper = new EventHandlerTaskAsyncHelper(OnAcquireAsync);
        application.AddOnAcquireRequestStateAsync(helper.BeginEventHandler, helper.EndEventHandler);
    }

    private async Task OnAcquireAsync(object? sender, EventArgs arguments)
    {
        await Task.Yield();
        Record(((HttpApplication)sender!).Context, "AcquireRequestStateAsync");
    }
}

public sealed class GatekeeperModule() : ModuleEventProbe("gatekeeper")
{
    private protected override void Extend(HttpApplication application) =>
        application.AuthenticateRequest += (sender, _) => Gate(((HttpApplication)sender!).Context);

    private static void Gate(HttpContext context)
    {
        if (context.Request.Path.IndexOf("/private/", StringComparison.OrdinalIgnoreCase) < 0
            || context.Request.Headers["X-Pass"] != null)
        {
            return;
        }

        context.Response.StatusCode = 403;
        context.Response.ContentType = "text/plain";
        context.Response.Write("DENIED-BY:gatekeeper");
        context.ApplicationInstance.CompleteRequest();
    }
}

public sealed class ResponseHeaderModule() : ModuleEventProbe("header")
{
    private protected override void Extend(HttpApplication application) =>
        application.BeginRequest += (sender, _) => ((HttpApplication)sender!).Context.Response
            .AppendHeader("X-Migrated", "header-module");
}

// The effective module list as the IIS rig read it off Modules.AllKeys: the merge's whole result,
// including the names no probe can speak for.
public sealed class ModuleListHandler : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "text/plain";
        context.Response.Write(
            "MODULES:" + string.Join("|", context.ApplicationInstance.Modules.AllKeys));
    }
}
