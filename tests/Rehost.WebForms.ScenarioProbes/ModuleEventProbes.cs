using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

// The five events the IIS modules rig recorded (MH1, MH10). Stages go to the witness rather than
// to response headers: a static file's response is already on the wire by EndRequest.
public abstract class ModuleEventProbe : IHttpModule
{
    private readonly string _name;

    private protected ModuleEventProbe(string name) => _name = name;

    public void Init(HttpApplication application)
    {
        application.BeginRequest += (sender, _) => Record(sender, "BeginRequest");
        application.AuthenticateRequest += (sender, _) => Record(sender, "AuthenticateRequest");
        application.AcquireRequestState += (sender, _) => Record(sender, "AcquireRequestState");
        application.PostRequestHandlerExecute += (sender, _) =>
            Record(sender, "PostRequestHandlerExecute");
        application.EndRequest += (sender, _) => Record(sender, "EndRequest");
        Extend(application);
    }

    private protected virtual void Extend(HttpApplication application)
    {
    }

    public void Dispose()
    {
    }

    private protected void Record(HttpContext context, string stage) =>
        Witness.Stage(context.Request, _name + ":" + stage);

    private void Record(object? sender, string stage) =>
        Record(((HttpApplication)sender!).Context, stage);
}

public sealed class UnconditionedModuleProbe() : ModuleEventProbe("unconditioned");

public sealed class ManagedHandlerModuleProbe() : ModuleEventProbe("managed-handler");

public sealed class WebServerCopyModuleProbe() : ModuleEventProbe("webserver-copy");

public sealed class ClassicCopyModuleProbe() : ModuleEventProbe("classic-copy");

public sealed class ClassicOnlyModuleProbe() : ModuleEventProbe("classic-only");

public sealed class DynamicModuleProbe() : ModuleEventProbe("dynamic");

// Rewrites at BeginRequest, ahead of the conditioned module's own step, so a request crosses the
// static/managed line mid-pipeline. Records no stages: the order tests here assert exact
// sequences and a fourth witness would move them.
public sealed class RewriteModuleProbe : IHttpModule
{
    private const string ToPage = "/rewrite-to-page.txt";
    private const string ToAsset = "/rewrite-to-asset.aspx";

    public void Init(HttpApplication application) =>
        application.BeginRequest += (sender, _) =>
        {
            var context = ((HttpApplication)sender!).Context;
            var filePath = context.Request.FilePath;

            if (filePath.EndsWith(ToPage, StringComparison.OrdinalIgnoreCase))
            {
                context.RewritePath("~/Default.aspx");
            }
            else if (filePath.EndsWith(ToAsset, StringComparison.OrdinalIgnoreCase))
            {
                context.RewritePath("~/asset.txt");
            }
        };

    public void Dispose()
    {
    }
}
