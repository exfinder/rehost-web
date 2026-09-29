#nullable enable

using System.Web.IisConfig;

namespace System.Web;

// Replaces IIS request filtering's hidden-segment 404.
internal static class HiddenSegments
{
    internal static bool Refuses(string? virtualPath)
    {
        if (string.IsNullOrEmpty(virtualPath))
        {
            return false;
        }

        var configuration = IisServerConfiguration.Current;
        foreach (var segment in virtualPath.Split('/'))
        {
            if (segment.Length != 0 && configuration.IsHiddenSegment(segment))
            {
                return true;
            }
        }

        return false;
    }
}
