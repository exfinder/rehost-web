namespace Rehost.WebForms.Hosting;

using System;
using System.Web;
using System.Web.Hosting;

// ApplicationManager activates the application around a registered object, the same shape IIS
// uses for ISAPIRuntime. Entering HttpRuntime from middleware instead would leave the hosting
// environment uninitialized.
internal sealed class ClassicPipelineDispatcher : MarshalByRefObject, IRegisteredObject
{
    internal void ProcessRequest(HttpWorkerRequest workerRequest)
    {
        HttpRuntime.ProcessRequest(workerRequest);
    }

    public void Stop(bool immediate)
    {
        // Releases the hosting environment's shutdown wait; without this the environment polls
        // until its shutdown timeout expires before force-stopping.
        HostingEnvironment.UnregisterObject(this);
    }
}
