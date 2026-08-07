#nullable enable

using System.Collections.Generic;

namespace System.Web;

// IIS request filtering refused, with a 404, any request whose path contains one of these
// segments, in any casing and at any depth — that is the only thing that kept App_Data (the
// canonical database and upload folder) and its siblings undownloadable, because the managed
// stack never carried the rule. The list is the <hiddenSegments> section of a default
// applicationHost.config (IIS 10.0.26100, 2026-08-07), minus web.config, whose requests the
// golden *.config forbidden-handler mapping already answers with Framework's managed 403.
internal static class HiddenSegments
{
    private static readonly HashSet<string> Segments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "App_code",
        "App_GlobalResources",
        "App_LocalResources",
        "App_WebReferences",
        "App_Data",
        "App_Browsers",
    };

    internal static void CheckVirtualPath(string? virtualPath)
    {
        if (string.IsNullOrEmpty(virtualPath))
        {
            return;
        }

        foreach (var segment in virtualPath.Split('/'))
        {
            if (Segments.Contains(segment))
            {
                throw new HttpException(404, string.Empty);
            }
        }
    }
}
