using System;
using System.Linq;
using System.Reflection;
using System.Web.Hosting;
using Rehost.Web.Parity.Contracts;
using Rehost.Web.Parity.Harness;

namespace Rehost.Web.Parity.Runner;

// One classic-pipeline parity session: activate the application through ApplicationManager,
// run the manifest steps through the registered IClassicPipelineRunner, drain, shut down.
// The oracle and the portable runtime differ only in the runner type and in what must happen
// before activation; the algorithm is this one.
public static class ClassicPipelineSession
{
    public static SessionObservation Run(
        SessionSpecification session,
        string applicationId,
        string applicationPath,
        Type runnerType,
        Assembly hostAssembly,
        Action? beforeActivation)
    {
        PhaseRunner.InPhase(
            "fixture-validation",
            () => FixtureValidator.Validate(applicationPath, hostAssembly));
        if (beforeActivation != null)
        {
            PhaseRunner.InPhase("host-registration", beforeActivation);
        }

        var manager = PhaseRunner.InPhase(
            "application-activation",
            ApplicationManager.GetApplicationManager);
        var applicationActivated = false;
        PhaseRunner.InPhase("application-activation", manager.Open);

        try
        {
            var registered = PhaseRunner.InPhase(
                "application-activation",
                () => manager.CreateObject(
                    applicationId,
                    runnerType,
                    "/",
                    PathUtilities.EnsureTrailingDirectorySeparator(applicationPath),
                    true,
                    true));
            applicationActivated = true;

            if (registered is not IClassicPipelineRunner runner)
            {
                throw new InvalidOperationException(
                    "ApplicationManager did not return an IClassicPipelineRunner.");
            }

            var observation = new SessionObservation { Name = session.Name };

            foreach (var step in session.Steps)
            {
                observation.Requests.AddRange(
                    PhaseRunner.InPhase(
                        "step:" + string.Join(",", step.Select(request => request.Name)),
                        () => runner.RunStep(step)));
            }

            // StopObject runs the registered object's shutdown notification while the
            // application is still callable; ShutdownApplication is what tears it down.
            PhaseRunner.InPhase(
                "application-cleanup",
                () => manager.StopObject(applicationId, runnerType));
            observation.ApplicationEvents = PhaseRunner.InPhase(
                "application-cleanup",
                runner.DrainApplicationEvents);
            observation.SessionEvents = PhaseRunner.InPhase(
                "application-cleanup",
                runner.DrainSessionEvents);
            applicationActivated = false;

            PhaseRunner.InPhase(
                "application-cleanup",
                () =>
                {
                    manager.ShutdownApplication(applicationId);
                    manager.Close();
                });

            return observation;
        }
        finally
        {
            if (applicationActivated)
            {
                PhaseRunner.InPhase(
                    "application-cleanup",
                    () =>
                    {
                        manager.StopObject(applicationId, runnerType);
                        manager.ShutdownApplication(applicationId);
                        manager.Close();
                    });
            }
        }
    }
}
