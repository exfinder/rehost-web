using System;
using System.Web;
using FrameworkOracle.Contracts;

namespace FrameworkOracle.Probes;

public sealed class ProbeModule : IHttpModule
{
    public void Init(HttpApplication context)
    {
        OracleEventJournal.Record("module.init");

        foreach (var name in context.Modules.AllKeys)
        {
            OracleEventJournal.Record("module.collection:" + name);
        }

        context.BeginRequest += OnBeginRequest;
        context.PreRequestHandlerExecute += OnPreRequestHandlerExecute;
        context.PostRequestHandlerExecute += OnPostRequestHandlerExecute;
        context.EndRequest += OnEndRequest;
    }

    public void Dispose()
    {
        OracleEventJournal.Record("module.dispose");
    }

    private static void OnBeginRequest(object sender, EventArgs eventArgs)
    {
        OracleEventJournal.Record("module.begin-request");
    }

    private static void OnPreRequestHandlerExecute(object sender, EventArgs eventArgs)
    {
        OracleEventJournal.Record("module.pre-request-handler-execute");
    }

    private static void OnPostRequestHandlerExecute(object sender, EventArgs eventArgs)
    {
        OracleEventJournal.Record("module.post-request-handler-execute");
    }

    private static void OnEndRequest(object sender, EventArgs eventArgs)
    {
        OracleEventJournal.Record("module.end-request");
    }
}
