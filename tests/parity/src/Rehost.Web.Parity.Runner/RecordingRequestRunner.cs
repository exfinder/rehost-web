using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Web;
using Rehost.Web.Parity.Contracts;

namespace Rehost.Web.Parity.Runner;

internal sealed class RecordingRequestRunner
{
    internal List<RequestObservation> RunStep(List<RequestSpecification> requests)
    {
        if (requests == null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        if (requests.Count == 0)
        {
            throw new ArgumentException("A step declares no requests.", nameof(requests));
        }

        foreach (var request in requests)
        {
            PipelineEvents.OpenRequest(request.Name);
        }

        ParityBarrier.Begin(requests.Count);

        var observations = new RequestObservation[requests.Count];

        if (requests.Count == 1)
        {
            observations[0] = Run(requests[0]);
            return new List<RequestObservation>(observations);
        }

        var failures = new Exception?[requests.Count];
        // Dedicated threads rather than the pool: a step exists to put requests in flight
        // together, and pool scheduling is free to run them one after another.
        var threads = new Thread[requests.Count];

        for (var index = 0; index < requests.Count; index++)
        {
            var slot = index;
            threads[slot] = new Thread(() =>
            {
                try
                {
                    observations[slot] = Run(requests[slot]);
                }
                catch (Exception exception)
                {
                    failures[slot] = exception;
                }
            })
            {
                IsBackground = true,
                Name = "parity:" + requests[slot].Name
            };
            threads[slot].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        foreach (var failure in failures)
        {
            if (failure != null)
            {
                throw failure;
            }
        }

        return new List<RequestObservation>(observations);
    }

    private static RequestObservation Run(RequestSpecification request)
    {
        var workerRequest = new RecordingWorkerRequest(request);
        ExceptionObservation? escapedException = null;

        PipelineEvents.Record(request.Name, "runner.process-request.enter");

        try
        {
            HttpRuntime.ProcessRequest(workerRequest);
            PipelineEvents.Record(request.Name, "runner.process-request.return");
        }
        catch (Exception exception)
        {
            escapedException = ExceptionObservation.FromException(exception);
            PipelineEvents.Record(request.Name, "runner.process-request.escape");
        }
        finally
        {
            ParityGate.Open(request.Name);
        }

        if (escapedException == null
            && !workerRequest.WaitForCompletion(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException(
                "EndOfRequest for '"
                + request.Name
                + "' did not complete within 30 seconds.");
        }

        return new RequestObservation
        {
            Name = request.Name,
            Observation = workerRequest.CreateObservation(
                PipelineEvents.DrainRequest(request.Name),
                escapedException)
        };
    }
}
