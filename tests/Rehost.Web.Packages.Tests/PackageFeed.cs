using System.Reflection;
using System.Xml.Linq;

namespace Rehost.Web.Packages.Tests;

public static class PackageFeed
{
    public static IReadOnlyList<CandidatePackage> Packages { get; } = Directory
        .GetFiles(Metadata("RehostPackageFeed"), "*.nupkg")
        .Select(CandidatePackage.Open)
        .OrderBy(p => p.Id, StringComparer.Ordinal)
        .ToList();

    public static CandidatePackage Package(string id) => Packages.Single(p => p.Id == id);

    public static IReadOnlyDictionary<string, string> CentralVersions { get; } = XDocument
        .Load(Metadata("RehostPackagesProps"))
        .Descendants("PackageVersion")
        .ToDictionary(e => (string)e.Attribute("Include")!, e => (string)e.Attribute("Version")!, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> BundledDependencies { get; } = XDocument
        .Load(Metadata("RehostDependenciesProps"))
        .Descendants("PackageReference")
        .Select(e => (string)e.Attribute("Include")!)
        .ToList();

    private static string Metadata(string key) => Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == key)
        .Value!;
}
