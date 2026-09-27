using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Packages.Tests;

public sealed class PackageContractTests
{
    private const string Bundle = "Rehost.WebForms";
    private const string Templates = "Rehost.WebForms.Templates";
    private const string TemplateRoot = "content/rehost-webforms/";

    private static readonly string[] PublicPackages =
    [
        "Rehost.AspNet.WebApi.WebHost",
        "Rehost.WebForms",
        "Rehost.WebForms.FriendlyUrls",
        "Rehost.WebForms.Hosting",
        "Rehost.WebForms.Optimization",
        "Rehost.WebForms.Optimization.WebForms",
        "Rehost.WebForms.Owin.Host.SystemWeb",
        "Rehost.WebForms.ScriptManager.Bundles",
        "Rehost.WebForms.Templates",
    ];

    private static readonly string[] UnpublishedComponents =
    [
        "Rehost.WebForms.Runtime",
        "Rehost.WebForms.ApplicationServices",
        "Rehost.WebForms.Extensions",
        "Rehost.WebForms.WebServices",
        "Rehost.Web.Infrastructure",
    ];

    private static readonly Dictionary<string, string[]> AspNetWebStackAssemblies = new()
    {
        ["Rehost.AspNet.WebApi.WebHost"] = ["Rehost.Web.Http.WebHost"],
    };

    private static IEnumerable<CandidatePackage> Satellites =>
        PackageFeed.Packages.Where(p => p.Id != Bundle && p.Id != Templates);

    [Fact]
    public void FeedHoldsTheNinePublicPackagesAtOneVersion()
    {
        PackageFeed.Packages.Select(p => p.Id).ShouldBe(PublicPackages.Order(StringComparer.Ordinal));
        PackageFeed.Packages.Select(p => p.Version).Distinct().ShouldHaveSingleItem();
        PackageFeed.Packages.Where(p => p.Id != Templates).ShouldAllBe(p => p.SymbolFiles.Any(f => f.EndsWith(".pdb")));
    }

    [Fact]
    public void EveryLibraryAssemblyCarriesTheFamilyVersion()
    {
        var packageVersion = PackageFeed.Packages[0].Version;
        var familyVersion = packageVersion.Split('-', '+')[0] + ".0";

        var versions = PackageFeed.Packages
            .SelectMany(p => p.LibraryFiles.Where(f => f.EndsWith(".dll", StringComparison.Ordinal)).Select(f => (Package: p, File: f)))
            .Select(entry =>
            {
                var path = entry.Package.ExtractToTemporaryFile(entry.File);
                try
                {
                    return (
                        Assembly: Path.GetFileName(entry.File),
                        AssemblyVersion: AssemblyName.GetAssemblyName(path).Version!.ToString(),
                        FileVersion: FileVersionInfo.GetVersionInfo(path).FileVersion);
                }
                finally
                {
                    File.Delete(path);
                }
            })
            .ToList();

        versions.Select(v => v.Assembly).Order(StringComparer.Ordinal).ShouldBe(
            [
                "Rehost.Web.Http.WebHost.dll",
                "Rehost.Web.Infrastructure.dll",
                "Rehost.WebForms.ApplicationServices.dll",
                "Rehost.WebForms.Extensions.dll",
                "Rehost.WebForms.FriendlyUrls.dll",
                "Rehost.WebForms.Hosting.dll",
                "Rehost.WebForms.Optimization.WebForms.dll",
                "Rehost.WebForms.Optimization.dll",
                "Rehost.WebForms.Owin.Host.SystemWeb.dll",
                "Rehost.WebForms.Runtime.dll",
                "Rehost.WebForms.ScriptManager.Bundles.dll",
                "Rehost.WebForms.WebServices.dll",
            ]);
        versions.ShouldAllBe(v => v.AssemblyVersion == familyVersion && v.FileVersion == familyVersion);
    }

    [Fact]
    public void BundleCarriesTheFiveComponentAssembliesAndNothingElseInLib()
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

        foreach (var satellite in Satellites)
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
        foreach (var satellite in Satellites)
        {
            var assemblies = AspNetWebStackAssemblies.GetValueOrDefault(satellite.Id, [satellite.Id]);

            satellite.LibraryFiles.Order(StringComparer.Ordinal).ShouldBe(
                assemblies.Select(a => $"lib/net10.0/{a}.dll").Order(StringComparer.Ordinal),
                satellite.Id);
        }
    }

    [Fact]
    public void TemplatesIsATemplatePackageCarryingOnlyItsContent()
    {
        var templates = PackageFeed.Package(Templates);

        templates.PackageTypes.ShouldBe(["Template"]);
        templates.LibraryFiles.ShouldBeEmpty();
        templates.Dependencies.ShouldBeEmpty();
        templates.Files.Where(f => f.StartsWith("content/")).Order(StringComparer.Ordinal).ShouldBe(
        [
            $"{TemplateRoot}.template.config/template.json",
            $"{TemplateRoot}MyApp.App/MyApp.App.csproj",
            $"{TemplateRoot}MyApp.Host/.gitignore",
            $"{TemplateRoot}MyApp.Host/MyApp.Host.csproj",
            $"{TemplateRoot}MyApp.Host/Program.cs",
            $"{TemplateRoot}MyApp.Host/Properties/launchSettings.json",
            $"{TemplateRoot}MyApp.Host/Web.Rehost.config",
            $"{TemplateRoot}MyApp.Rehost.slnx",
        ]);
    }

    [Fact]
    public void TemplateWritesPackageReferencesAtTheFeedVersion()
    {
        var templates = PackageFeed.Package(Templates);
        using var json = JsonDocument.Parse(templates.ReadText($"{TemplateRoot}.template.config/template.json"));
        var version = json.RootElement.GetProperty("symbols").GetProperty("rehostVersion");

        version.GetProperty("parameters").GetProperty("value").GetString().ShouldBe(templates.Version);

        var token = version.GetProperty("replaces").GetString()!;
        foreach (var project in new[] { "MyApp.App/MyApp.App.csproj", "MyApp.Host/MyApp.Host.csproj" })
        {
            var references = XDocument.Parse(templates.ReadText(TemplateRoot + project))
                .Descendants("PackageReference")
                .ToList();

            references.ShouldNotBeEmpty(project);
            references.ShouldAllBe(r => (string)r.Attribute("Version")! == token);
        }
    }

    [Fact]
    public void TemplateTransformRepeatsEveryDefaultAdjustment()
    {
        var defaults = Adjustments(PackageFeed.Package("Rehost.WebForms.Hosting").ReadText("build/Web.Rehost.config"));
        var template = Adjustments(PackageFeed.Package(Templates).ReadText($"{TemplateRoot}MyApp.Host/Web.Rehost.config"));

        defaults.ShouldNotBeEmpty();
        foreach (var adjustment in defaults)
        {
            template.ShouldContain(adjustment);
        }
    }

    private static List<string> Adjustments(string transform)
    {
        XNamespace xdt = "http://schemas.microsoft.com/XML-Document-Transform";

        return XDocument.Parse(transform)
            .Descendants()
            .Where(e => e.Attribute(xdt + "Transform") != null)
            .Select(e => string.Join(
                "/",
                e.AncestorsAndSelf().Reverse().Select(a => a.Name.LocalName)) + " " + string.Join(
                " ",
                e.Attributes().OrderBy(a => a.Name.ToString(), StringComparer.Ordinal).Select(a => $"{a.Name.LocalName}={a.Value}")))
            .ToList();
    }
}
