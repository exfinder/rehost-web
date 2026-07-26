using System;
using System.Web;
using CoreParity.Contracts;

namespace CoreParity.Probes;

public sealed class ProbeModule : IHttpModule
{
    public void Init(HttpApplication context)
    {
        PipelineEventJournal.Record("module.init");

        foreach (var name in context.Modules.AllKeys)
        {
            PipelineEventJournal.Record("module.collection:" + name);
        }

        context.BeginRequest += OnBeginRequest;
        context.PreRequestHandlerExecute += OnPreRequestHandlerExecute;
        context.PostRequestHandlerExecute += OnPostRequestHandlerExecute;
        context.EndRequest += OnEndRequest;
    }

    public void Dispose()
    {
        PipelineEventJournal.Record("module.dispose");
    }

    private static void OnBeginRequest(object? sender, EventArgs eventArgs)
    {
        PipelineEventJournal.Record("module.begin-request");
    }

    private static void OnPreRequestHandlerExecute(object? sender, EventArgs eventArgs)
    {
        PipelineEventJournal.Record("module.pre-request-handler-execute");
    }

    private static void OnPostRequestHandlerExecute(object? sender, EventArgs eventArgs)
    {
        PipelineEventJournal.Record("module.post-request-handler-execute");
    }

    private static void OnEndRequest(object? sender, EventArgs eventArgs)
    {
        PipelineEventJournal.Record("module.end-request");
    }
}
