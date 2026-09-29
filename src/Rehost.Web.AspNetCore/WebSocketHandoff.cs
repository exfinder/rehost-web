namespace Rehost.Web.AspNetCore;

using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Web;
using System.Web.Management;
using System.Web.Util;
using System.Web.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using HttpContext = System.Web.HttpContext;

// What IIS's integrated pipeline did after a request that called AcceptWebSocketRequest ran to
// its end (WebSocketPipeline.ProcessRequestImplAsync, minus the native pipe): finish the
// transition, slim the HttpContext, install the synchronization context, and run the
// application's callback over an AspNetWebSocket until it returns. The 101 itself is ASP.NET
// Core's accept, carrying the headers System.Web set (reading R-WS1: cookies and application
// headers, before and after the accept, ride the 101).
internal sealed class WebSocketHandoff : ISyncContext
{
    private readonly HttpContext _httpContext;
    private bool _processingComplete;

    private WebSocketHandoff(HttpContext httpContext)
    {
        _httpContext = httpContext;
    }

    internal static async Task RunAsync(
        Microsoft.AspNetCore.Http.HttpContext context,
        HttpContext httpContext,
        HttpWorkerRequest workerRequest,
        ResponseSpool spool,
        Func<AspNetWebSocketContext, Task> userFunc,
        string? subProtocol)
    {
        var feature = context.Features.Get<IHttpWebSocketFeature>()
            ?? throw new InvalidOperationException(
                "The WebSocket middleware is not registered; UseRehostWeb registers it.");

        foreach (var header in spool.Headers)
        {
            // The accept writes the upgrade set and the negotiated protocol itself; a
            // Content-Length has no place on a 101.
            if (header.Name is "Sec-WebSocket-Protocol" or "Connection" or "Upgrade" or "Content-Length")
            {
                continue;
            }

            context.Response.Headers.Append(header.Name, header.Value);
        }

        AspNetWebSocket? webSocket = null;
        try
        {
            // A client that abandons the handshake faults AcceptAsync; there is no socket to
            // clean up and nothing to surface but the connection loss.
            var socket = await feature.AcceptAsync(new WebSocketAcceptContext { SubProtocol = subProtocol });
            webSocket = new AspNetWebSocket(new KestrelWebSocketPipe(socket, context), subProtocol);
            var handoff = new WebSocketHandoff(httpContext);

            // The response cookie collection stays reachable from the request once the response
            // is gone (DevDiv 273639, the integrated transition step).
            httpContext.Request.StoreReferenceToResponseCookies(httpContext.Response.GetCookiesNoCreate());
            httpContext.TransitionToWebSocketState(WebSocketTransitionState.TransitionStarted);
            httpContext.CompleteTransitionToWebSocket();

            var syncContext = new AspNetSynchronizationContext(handoff);
            httpContext.SyncContext = syncContext;

            AspNetWebSocketManager.Current.Add(webSocket);
            try
            {
                Task? task = null;
                syncContext.Send(
                    _ => task = userFunc(new AspNetWebSocketContextImpl(
                        new HttpContextWrapper(httpContext), workerRequest, webSocket)),
                    null);
                syncContext.ExceptionDispatchInfo?.Throw();
                await task!.ConfigureAwait(false);
            }
            finally
            {
                handoff._processingComplete = true;
                webSocket.DisposeInternal();
                AspNetWebSocketManager.Current.Remove(webSocket);
            }
        }
        catch (Exception exception)
        {
            // Framework logged a callback failure and let the connection go rather than
            // surfacing anything: the 101 is long gone.
            WebBaseEvent.RaiseRuntimeError(exception, null);
        }

        if (webSocket != null)
        {
            // Pending I/O has to finish before the connection is torn down under it.
            await webSocket.AbortAsync().ConfigureAwait(false);
        }
    }

    HttpContext? ISyncContext.HttpContext => _processingComplete ? null : _httpContext;

    ISyncContextLock ISyncContext.Enter()
    {
        var threadContext = new ThreadContext(_httpContext);
        threadContext.AssociateWithCurrentThread(_httpContext.UsesImpersonation);
        return threadContext;
    }
}
