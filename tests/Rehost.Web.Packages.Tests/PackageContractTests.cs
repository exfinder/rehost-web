using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Rehost.Web.Packages.Tests;

public sealed class PackageContractTests
{
    private const string Bundle = "Rehost.Web";
    private const string WebPages = "Rehost.AspNet.WebPages";
    private const string Templates = "Rehost.Web.Templates";
    private const string TemplateRoot = "content/rehost-web/";

    private static readonly string[] PublicPackages =
    [
        "Rehost.AspNet.FriendlyUrls",
        "Rehost.AspNet.ScriptManager.MSAjax",
        "Rehost.AspNet.ScriptManager.WebForms",
        "Rehost.AspNet.Web.Optimization",
        "Rehost.AspNet.Web.Optimization.WebForms",
        "Rehost.AspNet.WebApi.WebHost",
        "Rehost.AspNet.WebPages",
        "Rehost.Owin.Host.SystemWeb",
        "Rehost.Web",
        "Rehost.Web.AspNetCore",
        "Rehost.Web.Templates",
    ];

    private static readonly string[] ComponentAssemblies =
    [
        "Rehost.Web",
        "Rehost.Web.ApplicationServices",
        "Rehost.Web.Extensions",
        "Rehost.Web.Services",
        "Rehost.Web.Infrastructure",
    ];

    private static readonly string[] UnpublishedComponents =
    [
        "Rehost.Web.Runtime",
        "Rehost.Web.ApplicationServices",
        "Rehost.Web.Extensions",
        "Rehost.Web.Services",
        "Rehost.Web.Infrastructure",
    ];

    private static readonly Dictionary<string, string[]> SatelliteAssemblies = new()
    {
        ["Rehost.AspNet.ScriptManager.MSAjax"] = ["Rehost.ScriptManager.MSAjax"],
        ["Rehost.AspNet.ScriptManager.WebForms"] = ["Rehost.ScriptManager.WebForms"],
        ["Rehost.AspNet.Web.Optimization"] = ["Rehost.Web.Optimization"],
        ["Rehost.AspNet.WebApi.WebHost"] = ["Rehost.Web.Http.WebHost"],
        [WebPages] = ["Rehost.Web.WebPages", "Rehost.Web.WebPages.Deployment", "Rehost.Web.WebPages.Razor"],
    };

    private static readonly string[] AssembliesReadingTheirOwnWebPagesVersion =
    [
        "Rehost.Web.WebPages.dll",
        "Rehost.Web.WebPages.Deployment.dll",
        "Rehost.Web.WebPages.Razor.dll",
    ];

    private static IEnumerable<CandidatePackage> Satellites =>
        PackageFeed.Packages.Where(p => p.Id != Bundle && p.Id != Templates);

    [Fact]
    public void FeedHoldsTheElevenPublicPackagesAtOneVersion()
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
                "Rehost.AspNet.FriendlyUrls.dll",
                "Rehost.AspNet.Web.Optimization.WebForms.dll",
                "Rehost.Owin.Host.SystemWeb.dll",
                "Rehost.ScriptManager.MSAjax.dll",
                "Rehost.ScriptManager.WebForms.dll",
                "Rehost.Web.ApplicationServices.dll",
                "Rehost.Web.AspNetCore.dll",
                "Rehost.Web.Extensions.dll",
                "Rehost.Web.Http.WebHost.dll",
                "Rehost.Web.Infrastructure.dll",
                "Rehost.Web.Optimization.dll",
                "Rehost.Web.Services.dll",
                "Rehost.Web.WebPages.Deployment.dll",
                "Rehost.Web.WebPages.Razor.dll",
                "Rehost.Web.WebPages.dll",
                "Rehost.Web.dll",
            ]);
        versions.ShouldAllBe(v => v.FileVersion == familyVersion);
        versions.Where(v => !AssembliesReadingTheirOwnWebPagesVersion.Contains(v.Assembly))
            .ShouldAllBe(v => v.AssemblyVersion == familyVersion);
        versions.Where(v => AssembliesReadingTheirOwnWebPagesVersion.Contains(v.Assembly))
            .Select(v => v.AssemblyVersion).ShouldBe(["3.0.0.0", "3.0.0.0", "3.0.0.0"]);
    }

    [Fact]
    public void BundleCarriesTheFiveComponentAssembliesAndNothingElseInLib()
    {
        var bundle = PackageFeed.Package(Bundle);

        bundle.LibraryFiles.Order(StringComparer.Ordinal).ShouldBe(
            ComponentAssemblies.Select(c => $"lib/net10.0/{c}.dll").Order(StringComparer.Ordinal));
        bundle.SymbolFiles.Where(f => f.StartsWith("lib/")).Order(StringComparer.Ordinal).ShouldBe(
            ComponentAssemblies.Select(c => $"lib/net10.0/{c}.pdb").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void BundleCarriesRootConfigurationAndTransitiveBuildAssets()
    {
        var bundle = PackageFeed.Package(Bundle);

        bundle.Files.ShouldContain("build/Rehost.Web.targets");
        bundle.Files.ShouldContain("buildTransitive/Rehost.Web.targets");
        bundle.Files.Where(f => f.StartsWith("contentFiles/any/any/configs/")).Order(StringComparer.Ordinal).ShouldBe(
        [
            "contentFiles/any/any/configs/DefaultWsdlHelpGenerator.aspx",
            "contentFiles/any/any/configs/rehost.applicationHost.config",
            "contentFiles/any/any/configs/rehost.machine.config",
            "contentFiles/any/any/configs/rehost.web.config",
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

        PackageFeed.Package("Rehost.AspNet.Web.Optimization.WebForms").Dependencies
            .ShouldContainKeyAndValue("Rehost.AspNet.Web.Optimization", version);
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
    public void WebPagesDependsOnTheBundleAndRazorUnderApache()
    {
        var webPages = PackageFeed.Package(WebPages);
        var license = XDocument.Parse(webPages.ReadText($"{WebPages}.nuspec"))
            .Descendants()
            .Single(e => e.Name.LocalName == "license");

        webPages.Dependencies.OrderBy(d => d.Key, StringComparer.Ordinal).ShouldBe(
        [
            new("Microsoft.AspNet.Razor", "3.3.0"),
            new(Bundle, PackageFeed.Package(Bundle).Version),
        ]);
        ((string)license.Attribute("type")!).ShouldBe("expression");
        license.Value.ShouldBe("Apache-2.0");
    }

    [Fact]
    public void HostingCarriesItsStagingToolingAndDefaultTransform()
    {
        var hosting = PackageFeed.Package("Rehost.Web.AspNetCore");

        hosting.Files.Where(f => f.StartsWith("build/")).Order(StringComparer.Ordinal).ShouldBe(
        [
            "build/Rehost.Web.AspNetCore.props",
            "build/Rehost.Web.AspNetCore.targets",
            "build/Web.Rehost.config",
            "build/tasks/Microsoft.Web.XmlTransform.dll",
            "build/tasks/Rehost.Web.Build.Tasks.dll",
        ]);
    }

    [Fact]
    public void EachSatelliteShipsExactlyItsOwnAssembly()
    {
        foreach (var satellite in Satellites)
        {
            var assemblies = SatelliteAssemblies.GetValueOrDefault(satellite.Id, [satellite.Id]);

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
        var defaults = Adjustments(PackageFeed.Package("Rehost.Web.AspNetCore").ReadText("build/Web.Rehost.config"));
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
