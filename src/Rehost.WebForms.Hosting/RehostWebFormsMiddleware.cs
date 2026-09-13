namespace Rehost.WebForms.Hosting;

using System;
using System.Text;
using System.Threading.Tasks;
using System.Web.IisConfig;
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
        // Every answer below is a response IIS's protocol module would have stamped, its own
        // refusals and the rewrite step's early answers included.
        CustomResponseHeaders.Register(context, _activation.CustomHeaders);

        // Kestrel's limit fires on a read, so whether the handler already ran would otherwise
        // depend on when the application first touches the entity. A chunked entity carries no
        // declared length and IIS never measured one against this limit, so only Content-Length
        // is judged here.
        if (context.Request.ContentLength > DeclaredLengthLimit(context))
        {
            var body = IisErrorBodies.Refusal(StatusCodes.Status413PayloadTooLarge);
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            context.Response.Headers.Connection = "close";
            context.Response.ContentType = "text/html";
            context.Response.ContentLength = Encoding.UTF8.GetByteCount(body);
            await context.Response.WriteAsync(body);
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

        string? rewrittenFrom = null;
        if (_activation.RewriteRules is { } rewriteRules)
        {
            var outcome = await rewriteRules.ApplyAsync(context);
            if (outcome.Kind == RewriteOutcomeKind.Answered)
            {
                return;
            }

            rewrittenFrom = outcome.OriginalUrl;
        }

        var dispatcher = _activation.Dispatcher;

        using var workerRequest = new AspNetCoreWorkerRequest(
            context,
            _activation.VirtualRootPath,
            _activation.PhysicalRootPath,
            ClassicPipelineActivation.TemporaryDirectory,
            rewrittenFrom);

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

    private long DeclaredLengthLimit(HttpContext context) =>
        Math.Min(
            _activation.RequestLimits.MaxAllowedContentLength,
            context.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize ?? long.MaxValue);
}
