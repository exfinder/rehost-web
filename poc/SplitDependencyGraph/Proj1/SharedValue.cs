using SplitSample.Proj2;

namespace SplitSample.Proj1;

public sealed record SharedValue(int Number)
{
    public static string NormalizeVersion(string value) => VersionProbe.Normalize(value);
}
