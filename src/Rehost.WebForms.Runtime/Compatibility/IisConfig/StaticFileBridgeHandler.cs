#nullable enable

using System.IO;
using System.Web.Util;

namespace System.Web.IisConfig;

// The base type is load-bearing: ImplicitAsyncPreloadModule and HttpServerUtility.Execute both
// type-test the handler for DefaultHttpHandler and must keep matching.
internal sealed class StaticFileBridgeHandler : DefaultHttpHandler
{
    public override IAsyncResult BeginProcessRequest(
        HttpContext context, AsyncCallback callback, object state)
    {
        var request = context.Request;

        if (request.HttpVerb == HttpVerb.POST)
        {
            throw new HttpException(
                405, SR.GetString(SR.Method_not_allowed, request.HttpMethod, request.Path));
        }

        if (DefaultHttpHandler.IsClassicAspRequest(request.FilePath))
        {
            throw new HttpException(403, SR.GetString(SR.Path_forbidden, request.Path));
        }

        if (!IisServerConfiguration.Current.ServesStaticContent(Path.GetExtension(request.FilePath))
            && !FileUtil.DirectoryExists(request.PhysicalPath))
        {
            throw new HttpException(404, string.Empty);
        }

        StaticFileHandler.ProcessRequestInternal(context, null);

        return new HttpAsyncResult(callback, state, true, null, null);
    }
}
