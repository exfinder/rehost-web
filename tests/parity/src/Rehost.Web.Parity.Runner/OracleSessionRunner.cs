using System;
using System.Collections.Generic;
using System.Web.Hosting;
using Rehost.Web.Parity.Contracts;

namespace Rehost.Web.Parity.Runner;

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
        PipelineEvents.RecordSession(immediate
            ? RunnerEvents.StopImmediate
            : RunnerEvents.Stop);
    }
}
