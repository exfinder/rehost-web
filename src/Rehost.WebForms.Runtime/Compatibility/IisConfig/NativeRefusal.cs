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
        response.AppendHeader("Connection", "close");
        if (status == 405)
        {
            response.AppendHeader("Allow", "GET, HEAD, OPTIONS, TRACE");
        }

        // Anything but a detailed body is the host's to write as the head leaves, where IIS's
        // custom-error module wrote it; the entity stays empty here for it to replace.
        if (IisServerConfiguration.Current.HttpErrors.DetailedFor(context.Request.IsLocal))
        {
            response.ContentType = "text/html";
            var detailLine = detail == null ? "" : $"<p>{HttpUtility.HtmlEncode(detail)}</p>";
            response.Write(IisErrorBodies.Refusal(status, detailLine));
        }

        context.ApplicationInstance.CompleteRequest();
    }
}

internal sealed class NativeRefusalHandler(int status, string? detail = null) : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context) => NativeRefusal.Respond(context, status, detail);
}
