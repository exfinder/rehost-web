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

    public void Stop(bool immediate)
    {
        PipelineEventJournal.Record(immediate
            ? "runner.stop.immediate"
            : "runner.stop");
    }
}
