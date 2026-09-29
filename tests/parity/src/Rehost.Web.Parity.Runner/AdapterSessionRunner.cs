using System;
using System.Collections.Generic;
using System.Web.Hosting;
using Rehost.Web.Parity.Contracts;

namespace Rehost.Web.Parity.Runner;

// Requests arrive over HTTP, so this object exists only to observe what no response can show:
// the application-wide event list and the terminal shutdown notification.
public sealed class AdapterSessionRunner : MarshalByRefObject, IRegisteredObject, IPipelineEventDrain
{
    public List<string> DrainApplicationEvents()
    {
        return PipelineEvents.DrainApplication();
    }

    public List<string> DrainSessionEvents()
    {
        var events = PipelineEvents.DrainSession();
        events.Add(RunnerEvents.ApplicationsCreated + PipelineEvents.ApplicationsCreated);
        return events;
    }

    public void Stop(bool immediate)
    {
        PipelineEvents.RecordSession(immediate ? RunnerEvents.StopImmediate : RunnerEvents.Stop);

        HostingEnvironment.UnregisterObject(this);
    }
}
