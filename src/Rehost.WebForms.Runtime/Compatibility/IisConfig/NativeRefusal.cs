#nullable enable

namespace System.Web.IisConfig;

// A refusal IIS answered from a native module is a response, never a managed exception:
// Application_Error and customErrors do not see it (MH40-MH42).
internal static class NativeRefusal
{
    internal static void Respond(HttpContext context, int status, string? detail = null)
    {
        var response = context.Response;
        response.Clear();
        response.SuppressDefaultCacheControlHeader = true;
        response.StatusCode = status;
        response.ContentType = "text/html";
        response.AppendHeader("Connection", "close");
        if (status == 405)
        {
            response.AppendHeader("Allow", "GET, HEAD, OPTIONS, TRACE");
        }
        // IIS's httpErrors default is DetailedLocalOnly: the path-bearing detail stays off the wire
        // for a remote client.
        var detailLine = detail == null || !context.Request.IsLocal ? "" : $"<p>{HttpUtility.HtmlEncode(detail)}</p>";
        response.Write(IisErrorBodies.Refusal(status, detailLine));
        context.ApplicationInstance.CompleteRequest();
    }
}

internal sealed class NativeRefusalHandler(int status, string? detail = null) : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context) => NativeRefusal.Respond(context, status, detail);
}
