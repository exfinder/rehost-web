#nullable enable

using System.Web.IisConfig;

namespace System.Web.Hosting;

// IIS split file path from path info by handler mapping, not by what exists on disk.
internal static class RequestPathInfo
{
    internal static (string FilePath, string PathInfo) Split(string verb, string virtualPath) =>
        Split(virtualPath, prefix => IsClaimedByHandler(verb, prefix));

    // Only segments carrying an extension can be claimed; a directory named with a dot is
    // asked and declines unless something maps it.
    internal static (string FilePath, string PathInfo) Split(
        string virtualPath,
        Func<string, bool> isClaimedByHandler)
    {
        if (string.IsNullOrEmpty(virtualPath))
        {
            return (virtualPath, "");
        }

        var start = 0;
        while (start < virtualPath.Length)
        {
            var next = virtualPath.IndexOf('/', start + 1);
            var end = next < 0 ? virtualPath.Length : next;
            var segmentHasExtension = virtualPath.LastIndexOf('.', end - 1, end - start) > start;
            if (segmentHasExtension && isClaimedByHandler(virtualPath[..end]))
            {
                return (virtualPath[..end], virtualPath[end..]);
            }

            if (next < 0)
            {
                break;
            }

            start = next;
        }

        return (virtualPath, "");
    }

    // A prefix the configuration system cannot even address (a ':' segment, say) claims nothing:
    // the whole URL stays the file path and the request fails where Framework failed it, on the
    // pipeline's own configuration lookup rather than inside the worker request. The catch-all
    // rows claim nothing either, which is what leaves an unmapped URL whole.
    private static bool IsClaimedByHandler(string verb, string prefix)
    {
        try
        {
            var route = IntegratedHandlers.Selected(verb, VirtualPath.Create(prefix));
            return route != null && route.Registration.Path != "*";
        }
        catch (Exception exception) when (exception is HttpException or ArgumentException)
        {
            return false;
        }
    }
}
