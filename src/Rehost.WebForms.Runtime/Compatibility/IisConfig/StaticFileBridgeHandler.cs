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
            NativeRefusal.Respond(context, 405);
            return new HttpAsyncResult(callback, state, true, null, null);
        }

        if (DefaultHttpHandler.IsClassicAspRequest(request.FilePath))
        {
            NativeRefusal.Respond(context, 403);
            return new HttpAsyncResult(callback, state, true, null, null);
        }

        // The extension gate is load-bearing: IIS refused extensions outside its static
        // content-type list, and this host replaces IIS, so without it any file the forbidden
        // mappings miss would download (a *.bak beside web.config).
        if (!IisServerConfiguration.Current.ServesStaticContent(Path.GetExtension(request.FilePath))
            && !FileUtil.DirectoryExists(request.PhysicalPath))
        {
            NativeRefusal.Respond(context, 404);
            return new HttpAsyncResult(callback, state, true, null, null);
        }

        StaticFileHandler.ProcessRequestInternal(context, null);

        return new HttpAsyncResult(callback, state, true, null, null);
    }
}
