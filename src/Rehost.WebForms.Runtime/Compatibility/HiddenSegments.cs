#nullable enable

using System.Web.IisConfig;

namespace System.Web;

// IIS request filtering's hidden-segment 404, any casing, any depth (ledger P59/P60). The
// list is the merged <hiddenSegments> section; apps extend or un-hide entries.
internal static class HiddenSegments
{
    internal static void CheckVirtualPath(string? virtualPath)
    {
        if (string.IsNullOrEmpty(virtualPath))
        {
            return;
        }

        var configuration = IisServerConfiguration.Current;
        foreach (var segment in virtualPath.Split('/'))
        {
            if (segment.Length != 0 && configuration.IsHiddenSegment(segment))
            {
                throw new HttpException(404, string.Empty);
            }
        }
    }
}
