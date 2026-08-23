#nullable enable

using System.Collections.Generic;
using System.Web.Configuration;

namespace System.Web.IisConfig;

// preCondition="managedHandler" is decided per request, not per pipeline: IIS ran such a module
// only when the handler it had already selected was managed, so an unconditioned module saw the
// static-file request and a conditioned one did not (MH10), unless
// runAllManagedModulesForAllRequests cleared the condition (MH17), which the snapshot resolves
// before the list reaches here.
//
// One instance per HttpApplication. Ownership is captured at hookup time, the way integrated mode
// routes subscriptions into per-module containers: while a module's Init runs, the application's
// current module key names it, and the delegates it subscribes are the ones this decides for.
internal sealed class ManagedHandlerModules
{
    private readonly HashSet<string> _modules = new(StringComparer.Ordinal);
    private readonly HashSet<Delegate> _events = new();

    internal void Add(string moduleName) => _modules.Add(moduleName);

    internal void Track(string? moduleName, Delegate? handler)
    {
        if (handler != null && moduleName != null && _modules.Contains(moduleName))
        {
            _events.Add(handler);
        }
    }

    internal HttpApplication.IExecutionStep Condition(
        HttpApplication application,
        HttpApplication.IExecutionStep step,
        Delegate? handler) =>
        handler != null && _events.Contains(handler)
            ? new ConditionedStep(application, step)
            : step;

    // The engine below this seam is the classic pipeline, which maps the handler halfway through
    // the request; IIS knew the mapping before BeginRequest. The answer is the same walk the
    // pipeline will run over the merged handler list, asked early: a row carrying type= is a
    // managed handler and a native-bridged row is not. A routed request never reaches the walk,
    // and its remapped instance is managed.
    private static bool IsManagedRequest(HttpApplication application)
    {
        var context = application.Context;
        if (context == null || context.RemapHandlerInstance != null)
        {
            return true;
        }

        return context.IsManagedHandlerRequest ??= WalkForArrivingPath(context);
    }

    // The arriving URL, which a rewrite does not move. Reading Request.FilePathObject would let
    // conditioned modules on either side of a rewrite disagree within one request.
    private static bool WalkForArrivingPath(HttpContext context)
    {
        var route = IntegratedHandlers.Selected(
            context.Request.RequestType,
            context.WorkerRequest?.GetFilePathObject() ?? context.Request.FilePathObject);

        return route != null && route.IsManaged;
    }

    private sealed class ConditionedStep : HttpApplication.IExecutionStep
    {
        private readonly HttpApplication _application;
        private readonly HttpApplication.IExecutionStep _step;
        private bool _skipped;

        internal ConditionedStep(
            HttpApplication application,
            HttpApplication.IExecutionStep step)
        {
            _application = application;
            _step = step;
        }

        void HttpApplication.IExecutionStep.Execute()
        {
            _skipped = !IsManagedRequest(_application);
            if (!_skipped)
            {
                _step.Execute();
            }
        }

        // An asynchronous step decides this inside Execute; a skipped one never ran, so the
        // pipeline must not wait for a completion that will not come.
        bool HttpApplication.IExecutionStep.CompletedSynchronously =>
            _skipped || _step.CompletedSynchronously;

        bool HttpApplication.IExecutionStep.IsCancellable => _step.IsCancellable;
    }
}
