#nullable enable

using System.Web.IisConfig;

namespace System.Web;

// Replaces IIS request filtering's hidden-segment 404 (ledger P59/P60).
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
