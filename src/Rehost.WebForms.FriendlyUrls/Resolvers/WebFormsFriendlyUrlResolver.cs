using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;

namespace Microsoft.AspNet.FriendlyUrls.Resolvers;

public class WebFormsFriendlyUrlResolver : FriendlyUrlResolver
{
    private const string IsMobileKey = "AspNet.FriendlyUrls.IsMobile";
    private const string MasterExtension = ".Master";
    private const string MobileSuffix = "Mobile";
    private const string MobileAspxExtension = ".Mobile.aspx";

    public static readonly string ViewSwitcherCookieName = "FriendlyUrlsViewSwitcher";

    private readonly Lazy<FriendlyUrlFileCache?> _fileCache;

    public WebFormsFriendlyUrlResolver()
        : this(ResolverCachingMode.Static)
    {
    }

    internal WebFormsFriendlyUrlResolver(ResolverCachingMode cachingMode)
        : base(".aspx")
    {
        _fileCache = FriendlyUrlFileCache.CreateLazy(cachingMode);
    }

    public static bool IsMobileView(HttpContextBase httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return (bool?)httpContext.Items[IsMobileKey] ?? false;
    }

    public override IList<string> GetExtensions(HttpContextBase? httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var userViewOverride = GetUserViewOverride(httpContext);
        if (userViewOverride != null)
        {
            return IsMobileViewName(userViewOverride)
                ? new[] { MobileAspxExtension, ".aspx" }
                : base.GetExtensions(httpContext);
        }

        return httpContext.Request.Browser.IsMobileDevice
            ? new[] { MobileAspxExtension, ".aspx" }
            : base.GetExtensions(httpContext);
    }

    public override void PreprocessRequest(
        HttpContextBase httpContext,
        IHttpHandler httpHandler)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpHandler is not Page page)
        {
            return;
        }

        if (IsMobileExtension(httpContext, httpContext.Request.GetFriendlyUrlFileExtension()))
        {
            SetIsMobileItemsFlag(httpContext);
        }

        page.PreInit += (_, _) => Page_PreInit(page, httpContext);
    }

    protected virtual bool IsMobileExtension(
        HttpContextBase httpContext,
        string extension)
    {
        return string.Equals(extension, MobileAspxExtension, StringComparison.OrdinalIgnoreCase);
    }

    private void Page_PreInit(Page page, HttpContextBase httpContext)
    {
        var userViewOverride = GetUserViewOverride(httpContext);
        if ((httpContext.Request.Browser.IsMobileDevice || userViewOverride != null) &&
            (userViewOverride == null || IsMobileViewName(userViewOverride)))
        {
            TrySetMobileMasterPage(httpContext, page, MobileSuffix);
        }
    }

    protected virtual bool TrySetMobileMasterPage(
        HttpContextBase httpContext,
        Page page,
        string mobileSuffix)
    {
        if (page == null || string.IsNullOrEmpty(page.MasterPageFile))
        {
            return false;
        }

        if (!page.MasterPageFile.EndsWith(MasterExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The master page file must have the extension '.Master'.");
        }

        var baseName = page.MasterPageFile.Remove(page.MasterPageFile.Length - MasterExtension.Length);
        var mobileMasterPage = baseName + "." + mobileSuffix + MasterExtension;
        var applied = TrySetMasterPageFile(page, mobileMasterPage);
        if (applied)
        {
            SetIsMobileItemsFlag(httpContext);
        }

        return applied;
    }

    protected internal virtual bool TrySetMasterPageFile(Page page, string masterPageFile)
    {
        var fileCache = _fileCache.Value;
        if (string.IsNullOrEmpty(masterPageFile) ||
            fileCache == null ||
            !fileCache.FileExists(VirtualPathUtility.ToAppRelative(masterPageFile)))
        {
            return false;
        }

        page.MasterPageFile = masterPageFile;
        return true;
    }

    private static void SetIsMobileItemsFlag(HttpContextBase httpContext)
    {
        httpContext.Items[IsMobileKey] = true;
    }

    private static bool IsMobileViewName(string view)
    {
        return string.Equals(view, MobileSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetUserViewOverride(HttpContextBase httpContext)
    {
        var view = (string?)httpContext.Items[ViewSwitcherCookieName];
        if (view == null)
        {
            view = httpContext.Request.Cookies[ViewSwitcherCookieName]?.Value;
            httpContext.Items[ViewSwitcherCookieName] = view;
        }

        return view;
    }
}
