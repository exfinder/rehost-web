using System.Collections.Generic;
using System.Web.Hosting;
using CoreParity.Contracts;
using CoreParity.Recording;

namespace PortableParity.Runner;

public sealed class PortableRunner : IRegisteredObject, IClassicPipelineRunner
{
    public PipelineObservation Run(RequestSpecification request)
    {
        return new RecordingRequestRunner().Run(request);
    }

    public List<string> DrainEvents()
    {
        return PipelineEventJournal.Drain();
    }

    public void Stop(bool immediate)
    {
        PipelineEventJournal.Record(immediate
            ? "runner.stop.immediate"
            : "runner.stop");

        // Releases the hosting environment's shutdown wait; without this the environment
        // polls until its shutdown timeout expires before force-stopping.
        HostingEnvironment.UnregisterObject(this);
    }
}
