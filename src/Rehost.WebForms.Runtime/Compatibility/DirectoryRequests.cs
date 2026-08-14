#nullable enable

using System.IO;
using System.Web.IisConfig;
using System.Web.Util;

namespace System.Web;

// Two IIS modules decided directory requests after routing and before handler mapping, and
// this host replaces IIS (ledger P67, readings D1-D15): DefaultDocumentModule rewrote to the
// first existing candidate and redirected slash-less directory URLs; when it declined, the
// request fell through to DirectoryListingModule, whose browsing-off answer was the 403. The
// classic engine has no fall-through module, so one step owns both halves. The rewritten
// path flows on through normal handler mapping.
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

        // Section first: a broken section refuses both slash forms without redirecting, and
        // a disabled section suppresses the courtesy redirect too (readings D14/D15).
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
                    // The app observes the list's spelling, not the disk's (reading D1).
                    context.RewritePath(path + file, rebaseClientPath: true);
                    return;
                }
            }
        }

        throw new HttpException(
            403,
            "The directory '" + path + "' has no default document and directory browsing is"
            + " not supported.");
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
