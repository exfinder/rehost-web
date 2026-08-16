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

        // A request that accepted a WebSocket and finished with the 101 hands its connection over;
        // any other status (an error page, a redirect) is committed as usual, as IIS's transition
        // step no-op'd unless the status was still 101.
        if (workerRequest.WebSocketAccept is { } accept
            && workerRequest.Response.StatusCode == StatusCodes.Status101SwitchingProtocols)
        {
            await WebSocketHandoff.RunAsync(
                context, accept.Context, workerRequest, workerRequest.Response, accept.UserFunc, accept.SubProtocol);
            return;
        }

        try
        {
            await workerRequest.Response.CommitAsync(context, context.RequestAborted);
        }
        catch (Exception exception) when (ResponseSpool.IsTransportFailure(exception))
        {
            // The client disconnected before the terminal commit finished; there is nothing left
            // to deliver. (A disconnect during an application flush already surfaced to the page
            // as an HttpException, and the spool no-ops here.)
            context.Abort();
        }
    }

    private static bool ExceedsHostBodyLimit(HttpContext context)
    {
        return context.Request.ContentLength is long declared
            && context.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize is long limit
            && declared > limit;
    }
}
