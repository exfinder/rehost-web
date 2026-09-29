#nullable enable

namespace System.Web.IisConfig;

// One rendering, so the runtime's refusal and the host's pre-pipeline answer stay byte-identical.
internal static class IisErrorBodies
{
    internal static string Refusal(int status, string detailLine = "")
    {
        var (title, text) = status switch
        {
            401 => ("401 - Unauthorized: Access is denied due to invalid credentials.",
                "You do not have permission to view this directory or page."),
            403 => ("403 - Forbidden: Access is denied.",
                "You do not have permission to view this directory or page."),
            405 => ("405 - HTTP verb used to access this page is not allowed.",
                "The page you are looking for cannot be displayed because an invalid method (HTTP verb) is being used."),
            406 => ("406 - Client browser does not accept the MIME type of the requested page.",
                "The page you are looking for cannot be opened by your browser because it has a file name extension that your browser does not accept."),
            412 => ("412 - Precondition set by the client failed when evaluated on the Web server.",
                "The request was not completed due to preconditions that are set in the request header."),
            413 => ("413 - Request entity too large.",
                "The page you are looking for cannot be displayed because the request entity is larger than the Web server is configured to allow."),
            431 => ("431 - Request header too long.",
                "One of the request headers is longer than the specified limit configured in the server."),
            500 => ("500 - Internal server error.",
                "The page cannot be displayed because an internal server error has occurred."),
            501 => ("501 - Header values specify a method that is not implemented.",
                "The page you are looking for cannot be displayed because a header value in the request does not match certain configuration settings on the Web server."),
            502 => ("502 - Web server received an invalid response while acting as a gateway or proxy server.",
                "There is a problem with the page you are looking for, and it cannot be displayed."),
            _ => ("404 - File or directory not found.",
                "The resource you are looking for has been removed, had its name changed, or is temporarily unavailable."),
        };

        return $"""
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
            """.ReplaceLineEndings("\n");
    }

    internal static string ObjectMoved(string location)
    {
        var href = Net.WebUtility.HtmlEncode(location);
        return $"""
            <head><title>Document Moved</title></head>
            <body><h1>Object Moved</h1>This document may be found <a href="{href}">here</a></body>
            """.ReplaceLineEndings("\n");
    }
}
