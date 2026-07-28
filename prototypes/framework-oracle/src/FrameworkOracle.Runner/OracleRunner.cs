using System;
using System.Collections.Generic;
using System.Web.Hosting;
using CoreParity.Contracts;
using CoreParity.Recording;

namespace FrameworkOracle.Runner;

public sealed class OracleRunner : MarshalByRefObject, IRegisteredObject, IClassicPipelineRunner
{
    public override object InitializeLifetimeService()
    {
        return null!;
    }

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
    }
}
