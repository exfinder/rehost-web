using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;

namespace Microsoft.AspNet.FriendlyUrls.Resolvers;

public class WebFormsFriendlyUrlResolver : FriendlyUrlResolver
{
    public static readonly string ViewSwitcherCookieName = "AspNet.FriendlyUrls.IsMobile";

    public WebFormsFriendlyUrlResolver()
        : base(".aspx")
    {
    }

    public static bool IsMobileView(HttpContextBase httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var cookie = httpContext.Request.Cookies[ViewSwitcherCookieName];
        if (cookie != null && bool.TryParse(cookie.Value, out var mobileOverride))
        {
            return mobileOverride;
        }

        return httpContext.Request.Browser.IsMobileDevice;
    }

    public override IList<string> GetExtensions(HttpContextBase? httpContext)
    {
        return httpContext != null && IsMobileView(httpContext)
            ? new[] { ".Mobile.aspx", ".aspx" }
            : new[] { ".aspx" };
    }

    public override void PreprocessRequest(
        HttpContextBase httpContext,
        IHttpHandler httpHandler)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(httpHandler);

        if (httpHandler is not Page page || !IsMobileView(httpContext))
        {
            return;
        }

        var extension = httpContext.Request.GetFriendlyUrlFileExtension();
        var mobileSuffix = IsMobileExtension(httpContext, extension ?? string.Empty)
            ? extension![1..^".aspx".Length]
            : "Mobile";

        page.PreInit += (_, _) => TrySetMobileMasterPage(
            httpContext,
            page,
            mobileSuffix);
    }

    protected virtual bool IsMobileExtension(
        HttpContextBase httpContext,
        string extension)
    {
        return string.Equals(extension, ".Mobile.aspx", StringComparison.OrdinalIgnoreCase);
    }

    protected virtual bool TrySetMobileMasterPage(
        HttpContextBase httpContext,
        Page page,
        string mobileSuffix)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(page);

        var masterPageFile = page.MasterPageFile;
        if (string.IsNullOrEmpty(masterPageFile))
        {
            return false;
        }

        var extension = VirtualPathUtility.GetExtension(masterPageFile);
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        var mobileMasterPage = masterPageFile[..^extension.Length] +
            "." + mobileSuffix + extension;
        return TrySetMasterPageFile(page, mobileMasterPage);
    }

    protected internal virtual bool TrySetMasterPageFile(
        Page page,
        string masterPageFile)
    {
        ArgumentNullException.ThrowIfNull(page);

        var provider = System.Web.Hosting.HostingEnvironment.VirtualPathProvider;
        if (provider == null || !provider.FileExists(masterPageFile))
        {
            return false;
        }

        page.MasterPageFile = masterPageFile;
        return true;
    }
}
