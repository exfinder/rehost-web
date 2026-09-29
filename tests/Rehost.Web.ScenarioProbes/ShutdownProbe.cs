using System.Globalization;
using System.Web.Hosting;
using Rehost.Web.ScenarioProtocol;

namespace Rehost.Web.ScenarioProbes;

// IV30: an integrated pool stop raises HostingEnvironment.StopListening and
// IStopListeningRegisteredObject.StopListening ahead of IRegisteredObject.Stop; an app-domain
// recycle raises neither.
public sealed class ShutdownProbe : IRegisteredObject, IStopListeningRegisteredObject
{
    public static void Arm()
    {
        // First in the multicast chain, so the recording subscriber below and the registered-object
        // loop after it are both lost to the throw — which is what the host has to survive.
        if (Environment.GetEnvironmentVariable(ShutdownProtocol.ThrowingSubscriberVariable) != null)
        {
            HostingEnvironment.StopListening += (_, _) =>
            {
                TraceChannel.RecordTo(
                    ShutdownProtocol.LogVariable, ShutdownProtocol.StopListeningThrew);
                throw new InvalidOperationException("stop-listening-subscriber");
            };
        }

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
