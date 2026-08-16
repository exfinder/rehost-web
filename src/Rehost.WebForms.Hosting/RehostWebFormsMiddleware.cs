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
        // Kestrel's limit fires on a read, so whether the handler already ran would otherwise
        // depend on when the application first touches the entity.
        if (ExceedsHostBodyLimit(context))
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        // http.sys refused a URL whose '..' climbs above the root before IIS or ASP.NET saw it;
        // Kestrel resolves it silently, so only the raw target still shows the climb.
        if (RequestPathCanonicalizer.EscapesRoot(
                context.Features.Get<IHttpRequestFeature>()?.RawTarget))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

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

    private static bool ExceedsHostBodyLimit(HttpContext context)
    {
        return context.Request.ContentLength is long declared
            && context.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize is long limit
            && declared > limit;
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

        await spool.CommitBodyAsync(response, context.RequestAborted);
    }
}
