using System;
using System.Web;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Parity.Probes;

public sealed class ProbeModule : IHttpModule
{
    public void Init(HttpApplication context)
    {
        PipelineEvents.CountApplication();
        PipelineEvents.RecordApplication("module.init");

        foreach (var name in context.Modules.AllKeys)
        {
            PipelineEvents.RecordApplication("module.collection:" + name);
        }

        context.BeginRequest += OnBeginRequest;
        context.PreRequestHandlerExecute += OnPreRequestHandlerExecute;
        context.PostRequestHandlerExecute += OnPostRequestHandlerExecute;
        context.EndRequest += OnEndRequest;
    }

    public void Dispose()
    {
        PipelineEvents.RecordApplication("module.dispose");
    }

    private static void OnBeginRequest(object? sender, EventArgs eventArgs)
    {
        var application = (HttpApplication)sender!;
        ProbeEvents.Record(application.Context, "module.begin-request");

        var path = application.Context.Request.Path;

        if (path.EndsWith("/complete", StringComparison.Ordinal))
        {
            ProbeEvents.Record(application.Context, "module.complete-request");
            application.CompleteRequest();
            return;
        }

        if (path.EndsWith("/throw-module", StringComparison.Ordinal))
        {
            ProbeEvents.Record(application.Context, "module.throw");
            throw new InvalidOperationException("Probe module failed deliberately.");
        }
    }

    private static void OnPreRequestHandlerExecute(object? sender, EventArgs eventArgs)
    {
        Record(sender, "module.pre-request-handler-execute");
    }

    private static void OnPostRequestHandlerExecute(object? sender, EventArgs eventArgs)
    {
        Record(sender, "module.post-request-handler-execute");
    }

    private static void OnEndRequest(object? sender, EventArgs eventArgs)
    {
        Record(sender, "module.end-request");
    }

    private static void Record(object? sender, string value)
    {
        ProbeEvents.Record(((HttpApplication)sender!).Context, value);
    }
}
