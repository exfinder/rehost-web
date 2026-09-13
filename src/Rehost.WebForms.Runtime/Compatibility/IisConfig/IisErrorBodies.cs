#nullable enable

namespace System.Web.IisConfig;

// One rendering, so the runtime's refusal and the host's pre-pipeline answer stay byte-identical.
internal static class IisErrorBodies
{
    internal static string Refusal(int status, string detailLine = "")
    {
        var (title, text) = status switch
        {
            403 => ("403 - Forbidden: Access is denied.",
                "You do not have permission to view this directory or page."),
            405 => ("405 - HTTP verb used to access this page is not allowed.",
                "The page you are looking for cannot be displayed because an invalid method (HTTP verb) is being used."),
            413 => ("413 - Request entity too large.",
                "The page you are looking for cannot be displayed because the request entity is larger than the Web server is configured to allow."),
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
