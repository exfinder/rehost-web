using System;
using System.Collections.Generic;
using System.Web.Hosting;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Parity.Runner;

public sealed class OracleSessionRunner : MarshalByRefObject, IRegisteredObject, IClassicPipelineRunner
{
    public override object InitializeLifetimeService()
    {
        return null!;
    }

    public List<RequestObservation> RunStep(List<RequestSpecification> requests)
    {
        return new RecordingRequestRunner().RunStep(requests);
    }

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
        PipelineEventJournal.RecordSession(immediate
            ? "runner.stop.immediate"
            : "runner.stop");
    }
}
