using System;
using System.Collections.Generic;
using System.Web.Hosting;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Parity.AdapterRunner;

// Requests arrive over HTTP, so this object exists only to observe what no response can show:
// the application-wide journal and the terminal shutdown notification.
public sealed class AdapterSessionRunner : MarshalByRefObject, IRegisteredObject, IPipelineEventDrain
{
    public List<string> DrainApplicationEvents()
    {
        return PipelineEventJournal.DrainApplication();
    }

    public List<string> DrainSessionEvents()
    {
        var events = PipelineEventJournal.DrainSession();
        events.Add("applications-created:" + PipelineEventJournal.ApplicationsCreated);
        return events;
    }

    public void Stop(bool immediate)
    {
        PipelineEventJournal.RecordSession(immediate ? "runner.stop.immediate" : "runner.stop");

        HostingEnvironment.UnregisterObject(this);
    }
}
