using System;
using System.Web;
using CoreParity.Contracts;

namespace CoreParity.Probes;

public sealed class ProbeModule : IHttpModule
{
    public void Init(HttpApplication context)
    {
        PipelineEventJournal.CountApplication();
        PipelineEventJournal.RecordSession("module.init");

        foreach (var name in context.Modules.AllKeys)
        {
            PipelineEventJournal.RecordSession("module.collection:" + name);
        }

        context.BeginRequest += OnBeginRequest;
        context.PreRequestHandlerExecute += OnPreRequestHandlerExecute;
        context.PostRequestHandlerExecute += OnPostRequestHandlerExecute;
        context.EndRequest += OnEndRequest;
    }

    public void Dispose()
    {
        PipelineEventJournal.RecordSession("module.dispose");
    }

    private static void OnBeginRequest(object? sender, EventArgs eventArgs)
    {
        Record(sender, "module.begin-request");
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
        ProbeJournal.Record(((HttpApplication)sender!).Context, value);
    }
}
