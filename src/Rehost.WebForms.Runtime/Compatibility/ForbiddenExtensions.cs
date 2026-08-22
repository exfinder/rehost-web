#nullable enable

using System.Web.IisConfig;
using System.Web.Util;

namespace System.Web;

// IIS request filtering answered 404 for the source, project and data extensions its
// <fileExtensions> deny list names, and this host replaces IIS. System.Web's classic
// <httpHandlers> table covered most of the same extensions with HttpForbiddenHandler's 403; the
// webServer handler walk retires that table, and the deny list is the rule IIS itself applied.
internal static class ForbiddenExtensions
{
    internal static void CheckVirtualPath(string? virtualPath)
    {
        if (!string.IsNullOrEmpty(virtualPath)
            && IisServerConfiguration.Current.IsForbiddenExtension(
                UrlPath.GetExtension(virtualPath!)))
        {
            throw new HttpException(404, string.Empty);
        }
    }
}
