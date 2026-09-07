#nullable enable

namespace System.Web.IisConfig;

// A refusal IIS answered from a native module is a response, never a managed exception:
// Application_Error and customErrors do not see it (MH40-MH42).
internal static class NativeRefusal
{
    internal static void Respond(HttpContext context, int status, string? detail = null)
    {
        var (title, text) = status switch
        {
            403 => ("403 - Forbidden: Access is denied.",
                "You do not have permission to view this directory or page using the credentials that you supplied."),
            405 => ("405 - HTTP verb used to access this page is not allowed.",
                "The page you are looking for cannot be displayed because an invalid method (HTTP verb) was used to attempt access."),
            _ => ("404 - File or directory not found.",
                "The resource you are looking for might have been removed, had its name changed, or is temporarily unavailable."),
        };

        var response = context.Response;
        response.Clear();
        response.StatusCode = status;
        response.ContentType = "text/html";
        var detailLine = detail == null ? "" : "<p>" + HttpUtility.HtmlEncode(detail) + "</p>";
        response.Write($"""
            <!DOCTYPE html>
            <html>
            <head><title>{title}</title></head>
            <body>
            <h1>Server Error</h1>
            <h2>{title}</h2>
            <p>{text}</p>
            {detailLine}
            </body>
            </html>
            """.ReplaceLineEndings("\n"));
        context.ApplicationInstance.CompleteRequest();
    }
}

internal sealed class NativeRefusalHandler(int status, string? detail = null) : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context) => NativeRefusal.Respond(context, status, detail);
}
