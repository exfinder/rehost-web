#nullable enable

using System.IO;
using System.Web.IisConfig;
using System.Web.Util;

namespace System.Web;

// Replaces IIS's DefaultDocument and DirectoryListing modules in one step.
internal sealed class DirectoryRequestExecutionStep : HttpApplication.IExecutionStep
{
    private readonly HttpApplication _application;

    internal DirectoryRequestExecutionStep(HttpApplication application)
    {
        _application = application;
    }

    void HttpApplication.IExecutionStep.Execute()
    {
        var context = _application.Context;
        var request = context.Request;

        if (context.RemapHandlerInstance != null || request.RewrittenUrl != null)
        {
            return;
        }

        var physicalPath = request.PhysicalPathInternal;
        if (string.IsNullOrEmpty(physicalPath) || !FileUtil.DirectoryExists(physicalPath))
        {
            return;
        }

        // Enabled gate first: a disabled section suppresses the courtesy redirect too
        // (reading D14).
        var documents = IisServerConfiguration.Current.DefaultDocuments;
        var path = request.Path;
        if (documents.Enabled)
        {
            if (!path.EndsWith('/'))
            {
                RedirectAppendingSlash(context);
                return;
            }

            foreach (var file in documents.Files)
            {
                var candidate = CanonicalCasePath.Resolve(
                    Path.Combine(physicalPath, file), HttpRuntime.AppDomainAppPathInternal);
                if (File.Exists(candidate))
                {
                    // The app observes the list's spelling, not the disk's (reading D1), while
                    // ClientFilePath stays the client's URL so a server form posts back to "./"
                    // as under IIS, not to the rewritten name.
                    context.RewritePath(path + file, rebaseClientPath: false);
                    return;
                }
            }
        }

        // IIS's browsing-off 403 was native, so managed customErrors never converted it
        // (reading D16): a completed response, not a thrown HttpException, keeps that surface.
        context.Response.StatusCode = 403;
        _application.CompleteRequest();
    }

    // 301 with an absolute Location and the query preserved (reading D5). Built from the raw
    // URL so the client's own escaping survives the round trip.
    private void RedirectAppendingSlash(HttpContext context)
    {
        var request = context.Request;
        var rawUrl = request.EnsureRawUrl();
        var query = rawUrl.IndexOf('?');
        var target = query < 0
            ? rawUrl + "/"
            : string.Concat(rawUrl.AsSpan(0, query), "/", rawUrl.AsSpan(query));

        var response = context.Response;
        response.StatusCode = 301;
        response.RedirectLocation = request.Url.GetLeftPart(UriPartial.Authority) + target;
        _application.CompleteRequest();
    }

    bool HttpApplication.IExecutionStep.CompletedSynchronously => true;

    bool HttpApplication.IExecutionStep.IsCancellable => false;
}
