namespace Rehost.WebForms.Hosting;

using System;
using System.IO;
using System.Text;
using System.Web.IisConfig;
using Microsoft.AspNetCore.Http;

// IIS's custom-error module judged the response as the head left, and replaced the entity alone:
// the status and its headers are the answer's, and later writes flow through untouched.
internal static class HttpErrorPages
{
    internal static void Apply(
        ResponseSpool spool, HttpContext context, HttpErrors config, bool skip)
    {
        var status = spool.StatusCode;
        if (status < StatusCodes.Status400BadRequest)
        {
            return;
        }

        if (config.DetailedFor(IsLocal(context)))
        {
            if (!spool.HasEntity)
            {
                BuiltIn(spool, status);
            }

            return;
        }

        if (config.ExistingResponse == ExistingResponse.PassThrough
            || (config.ExistingResponse == ExistingResponse.Auto && skip))
        {
            return;
        }

        // The spool carries no substatus, so only a row IIS would have matched with the wildcard
        // can answer here.
        var row = config.Find(status, 0);
        if (row is { Mode: HttpErrorRowMode.Redirect })
        {
            spool.Redirect(Absolute(context.Request, row.Location!));
            return;
        }

        if (row is { Mode: HttpErrorRowMode.File })
        {
            var file = new FileInfo(row.PhysicalPath!);
            if (file.Exists)
            {
                spool.ReplaceEntityWithFile(row.ContentType!, file.FullName, file.Length);
                return;
            }
        }

        BuiltIn(spool, status);
    }

    private static void BuiltIn(ResponseSpool spool, int status) =>
        spool.ReplaceEntity(
            "text/html", Encoding.UTF8.GetBytes(IisErrorBodies.Refusal(status)));

    private static string Absolute(HttpRequest request, string location) =>
        location.StartsWith('/') ? RequestUrls.Absolute(request, location) : location;

    private static bool IsLocal(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrEmpty(remote))
        {
            return false;
        }

        return remote is "127.0.0.1" or "::1"
            || string.Equals(
                remote,
                context.Connection.LocalIpAddress?.ToString(),
                StringComparison.Ordinal);
    }
}
