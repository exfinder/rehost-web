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
    }

    public void Dispose()
    {
    }

    private void Record(object? sender, string stage) =>
        Witness.Stage(((HttpApplication)sender!).Context.Request, _name + ":" + stage);
}

public sealed class UnconditionedModuleProbe() : ModuleEventProbe("unconditioned");

public sealed class ManagedHandlerModuleProbe() : ModuleEventProbe("managed-handler");

public sealed class WebServerCopyModuleProbe() : ModuleEventProbe("webserver-copy");

public sealed class ClassicCopyModuleProbe() : ModuleEventProbe("classic-copy");

public sealed class ClassicOnlyModuleProbe() : ModuleEventProbe("classic-only");

public sealed class DynamicModuleProbe() : ModuleEventProbe("dynamic");
