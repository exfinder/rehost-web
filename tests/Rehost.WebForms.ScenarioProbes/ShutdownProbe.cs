using System.Globalization;
using System.Web.Hosting;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.ScenarioProbes;

// IV30: an integrated pool stop raises HostingEnvironment.StopListening and
// IStopListeningRegisteredObject.StopListening ahead of IRegisteredObject.Stop; an app-domain
// recycle raises neither.
public sealed class ShutdownProbe : IRegisteredObject, IStopListeningRegisteredObject
{
    public static void Arm()
    {
        HostingEnvironment.StopListening +=
            (_, _) => TraceChannel.RecordTo(
                ShutdownProtocol.LogVariable, ShutdownProtocol.StopListeningEvent);
        HostingEnvironment.RegisterObject(new ShutdownProbe());
    }

    public void StopListening() =>
        TraceChannel.RecordTo(ShutdownProtocol.LogVariable, ShutdownProtocol.StopListeningObject);

    public void Stop(bool immediate)
    {
        TraceChannel.RecordTo(
            ShutdownProtocol.LogVariable,
            ShutdownProtocol.RegisteredStop + immediate.ToString(CultureInfo.InvariantCulture));

        // Without this the hosting environment polls until its shutdown timeout expires before
        // force-stopping, which would add half a minute to every host in this fixture.
        HostingEnvironment.UnregisterObject(this);
    }
}
