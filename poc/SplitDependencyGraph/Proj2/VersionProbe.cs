using NuGet.Versioning;

namespace SplitSample.Proj2;

public static class VersionProbe
{
    public static string Normalize(string value) => NuGetVersion.Parse(value).ToNormalizedString();
}
