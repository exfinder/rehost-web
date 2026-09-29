namespace Rehost.Web.AspNetCore;

using System;
using System.IO;
using System.Text;
using System.Web.IisConfig;
using Microsoft.AspNetCore.Http;

internal static class HttpErrorPages
{
    internal static void Apply(
        ResponseSpool spool, HttpContext context, HttpErrors config, bool skip, bool isLocal)
    {
        var status = spool.StatusCode;
        if (status < StatusCodes.Status400BadRequest)
        {
            return;
        }

        if (config.DetailedFor(isLocal))
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
            spool.Redirect(RequestUrls.Resolve(context.Request, row.Location!));
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

}
