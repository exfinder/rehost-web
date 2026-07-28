namespace Rehost.WebForms.Hosting;

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

internal sealed class RehostWebFormsMiddleware
{
    private readonly ClassicPipelineActivation _activation;

    internal RehostWebFormsMiddleware(ClassicPipelineActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation);

        _activation = activation;
    }

    internal async Task InvokeAsync(HttpContext context)
    {
        var dispatcher = _activation.Dispatcher;

        using var workerRequest = new AspNetCoreWorkerRequest(
            context,
            _activation.VirtualRootPath,
            _activation.PhysicalRootPath,
            ClassicPipelineActivation.TemporaryDirectory);

        try
        {
            dispatcher.ProcessRequest(workerRequest);
        }
        catch (Exception exception)
        {
            // An escape before the pipeline took ownership means EndOfRequest will never be
            // raised. Faulting is a no-op once the pipeline has already completed the request,
            // which is what makes a System.Web-owned failure complete normally.
            workerRequest.FailCompletion(exception);
        }

        await workerRequest.Completion;
        await CommitAsync(context, workerRequest.Response);
    }

    private static async Task CommitAsync(HttpContext context, ResponseSpool spool)
    {
        var response = context.Response;
        response.StatusCode = spool.StatusCode;

        // Without this the server substitutes the standard reason for the status code, and a
        // handler's own description is lost.
        var responseFeature = context.Features.Get<IHttpResponseFeature>();
        if (responseFeature != null)
        {
            responseFeature.ReasonPhrase = spool.ReasonPhrase;
        }

        foreach (var header in spool.Headers)
        {
            response.Headers.Append(header.Name, header.Value);
        }

        if (spool.ContentLength.HasValue)
        {
            response.ContentLength = spool.ContentLength;
        }

        await spool.DrainAsync(response.Body, context.RequestAborted);
    }
}
