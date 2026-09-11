using Shouldly;
using Xunit;

namespace Rehost.WebForms.Packages.Tests;

public sealed class PackageContractTests
{
    private const string Bundle = "Rehost.WebForms";

    private static readonly string[] PublicPackages =
    [
        "Rehost.WebForms",
        "Rehost.WebForms.FriendlyUrls",
        "Rehost.WebForms.Hosting",
        "Rehost.WebForms.Optimization",
        "Rehost.WebForms.Optimization.WebForms",
        "Rehost.WebForms.Owin.Host.SystemWeb",
        "Rehost.WebForms.ScriptManager.Bundles",
    ];

    private static readonly string[] UnpublishedComponents =
    [
        "Rehost.WebForms.Runtime",
        "Rehost.WebForms.ApplicationServices",
        "Rehost.WebForms.Extensions",
        "Rehost.WebForms.WebServices",
    ];

    [Fact]
    public void FeedHoldsTheSevenPublicPackagesAtOneVersion()
    {
        PackageFeed.Packages.Select(p => p.Id).ShouldBe(PublicPackages.Order(StringComparer.Ordinal));
        PackageFeed.Packages.Select(p => p.Version).Distinct().ShouldHaveSingleItem();
        PackageFeed.Packages.ShouldAllBe(p => p.SymbolFiles.Any(f => f.EndsWith(".pdb")));
    }

    [Fact]
    public void BundleCarriesTheFourComponentAssembliesAndNothingElseInLib()
    {
        var bundle = PackageFeed.Package(Bundle);

        bundle.LibraryFiles.Order(StringComparer.Ordinal).ShouldBe(
            UnpublishedComponents.Select(c => $"lib/net10.0/{c}.dll").Order(StringComparer.Ordinal));
        bundle.SymbolFiles.Where(f => f.StartsWith("lib/")).Order(StringComparer.Ordinal).ShouldBe(
            UnpublishedComponents.Select(c => $"lib/net10.0/{c}.pdb").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void BundleCarriesRootConfigurationAndTransitiveBuildAssets()
    {
        var bundle = PackageFeed.Package(Bundle);

        bundle.Files.ShouldContain("build/Rehost.WebForms.targets");
        bundle.Files.ShouldContain("buildTransitive/Rehost.WebForms.targets");
        bundle.Files.Where(f => f.StartsWith("contentFiles/any/any/configs/")).Order(StringComparer.Ordinal).ShouldBe(
        [
            "contentFiles/any/any/configs/DefaultWsdlHelpGenerator.aspx",
            "contentFiles/any/any/configs/rehost-webforms.applicationHost.config",
            "contentFiles/any/any/configs/rehost-webforms.machine.config",
            "contentFiles/any/any/configs/rehost-webforms.web.config",
        ]);
    }

    [Fact]
    public void BundleDependsOnEveryExternalPackageTheComponentsUse()
    {
        var bundle = PackageFeed.Package(Bundle);

        bundle.Dependencies.Keys.Order(StringComparer.OrdinalIgnoreCase).ShouldBe(
            PackageFeed.BundledDependencies.Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void NoPublicPackageDependsOnAnUnpublishedComponent()
    {
        foreach (var package in PackageFeed.Packages)
        {
            package.Dependencies.Keys.ShouldNotContain(
                id => UnpublishedComponents.Contains(id, StringComparer.OrdinalIgnoreCase),
                $"{package.Id} depends on an unpublished component");
        }
    }

    [Fact]
    public void EverySatelliteDependsOnTheBundleAtTheFeedVersion()
    {
        var version = PackageFeed.Package(Bundle).Version;

        foreach (var satellite in PackageFeed.Packages.Where(p => p.Id != Bundle))
        {
            satellite.Dependencies.ShouldContainKeyAndValue(Bundle, version, satellite.Id);
        }

        PackageFeed.Package("Rehost.WebForms.Optimization.WebForms").Dependencies
            .ShouldContainKeyAndValue("Rehost.WebForms.Optimization", version);
    }

    [Fact]
    public void RehostDependenciesNeverCrossTheFeedVersion()
    {
        var version = PackageFeed.Package(Bundle).Version;

        foreach (var package in PackageFeed.Packages)
        {
            foreach (var (id, dependencyVersion) in package.Dependencies.Where(d => d.Key.StartsWith("Rehost.", StringComparison.Ordinal)))
            {
                dependencyVersion.ShouldBe(version, $"{package.Id} -> {id}");
            }
        }
    }

    [Fact]
    public void ThirdPartyDependenciesKeepTheirCentralVersions()
    {
        foreach (var package in PackageFeed.Packages)
        {
            foreach (var (id, dependencyVersion) in package.Dependencies.Where(d => !d.Key.StartsWith("Rehost.", StringComparison.Ordinal)))
            {
                PackageFeed.CentralVersions.ShouldContainKey(id, $"{package.Id} -> {id}");
                dependencyVersion.ShouldBe(PackageFeed.CentralVersions[id], $"{package.Id} -> {id}");
            }
        }
    }

    [Fact]
    public void HostingCarriesItsStagingToolingAndDefaultTransform()
    {
        var hosting = PackageFeed.Package("Rehost.WebForms.Hosting");

        hosting.Files.Where(f => f.StartsWith("build/")).Order(StringComparer.Ordinal).ShouldBe(
        [
            "build/Rehost.WebForms.Hosting.props",
            "build/Rehost.WebForms.Hosting.targets",
            "build/Web.Rehost.config",
            "build/tasks/Microsoft.Web.XmlTransform.dll",
            "build/tasks/Rehost.WebForms.Build.Tasks.dll",
        ]);
    }

    [Fact]
    public void EachSatelliteShipsExactlyItsOwnAssembly()
    {
        foreach (var satellite in PackageFeed.Packages.Where(p => p.Id != Bundle))
        {
            satellite.LibraryFiles.ShouldBe([$"lib/net10.0/{satellite.Id}.dll"], satellite.Id);
        }
    }
}
