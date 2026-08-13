using System.Collections.Generic;
using System.Web.Hosting;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Parity.Runner;

public sealed class PortableSessionRunner : IRegisteredObject, IClassicPipelineRunner
{
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
        events.Add(RunnerEvents.ApplicationsCreated + PipelineEventJournal.ApplicationsCreated);
        return events;
    }

    public void Stop(bool immediate)
    {
        PipelineEventJournal.RecordSession(immediate
            ? RunnerEvents.StopImmediate
            : RunnerEvents.Stop);

        // Releases the hosting environment's shutdown wait; without this the environment
        // polls until its shutdown timeout expires before force-stopping.
        HostingEnvironment.UnregisterObject(this);
    }
}
