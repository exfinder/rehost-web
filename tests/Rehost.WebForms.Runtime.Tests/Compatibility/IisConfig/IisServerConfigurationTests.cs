using System.Configuration;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The measured IIS collection semantics (readings C1-C3), against real temp files because the
// loader's contract includes which file a failure names.
public sealed class IisServerConfigurationTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-iisconfig-");

    public void Dispose() => _root.Delete(recursive: true);

    private string WriteConfig(string name, string systemWebServerContent)
    {
        var path = Path.Combine(_root.FullName, name);
        File.WriteAllText(
            path,
            "<?xml version=\"1.0\"?><configuration><system.webServer>"
            + systemWebServerContent
            + "</system.webServer></configuration>");
        return path;
    }

    private string Baseline() => WriteConfig(
        "baseline.config",
        "<staticContent>"
        + "<mimeMap fileExtension=\".css\" mimeType=\"text/css\" />"
        + "<mimeMap fileExtension=\".js\" mimeType=\"application/javascript\" />"
        + "</staticContent>"
        + "<security><requestFiltering><hiddenSegments>"
        + "<add segment=\"App_Data\" />"
        + "</hiddenSegments></requestFiltering></security>");

    [Fact]
    public void Amendments_Merge_Over_The_Baseline()
    {
        var app = WriteConfig(
            "web.config",
            "<staticContent>"
            + "<remove fileExtension=\".css\" />"
            + "<mimeMap fileExtension=\".probe\" mimeType=\"application/x-probe\" />"
            + "</staticContent>"
            + "<security><requestFiltering><hiddenSegments>"
            + "<remove segment=\"App_Data\" /><add segment=\"Private\" />"
            + "</hiddenSegments></requestFiltering></security>");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.ServesStaticContent(".css").ShouldBeFalse();
        configuration.ServesStaticContent(".probe").ShouldBeTrue();
        configuration.StaticContentTypeOf(".PROBE").ShouldBe("application/x-probe");
        configuration.StaticContentTypeOf(".js").ShouldBe("application/javascript");
        configuration.IsHiddenSegment("App_Data").ShouldBeFalse();
        configuration.IsHiddenSegment("private").ShouldBeTrue();
    }

    [Fact]
    public void A_Duplicate_Add_Fails_Naming_The_File_As_IIS_Refuses_It()
    {
        var app = WriteConfig(
            "web.config",
            "<staticContent><mimeMap fileExtension=\".css\" mimeType=\"text/css\" /></staticContent>");

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain(".css");
        exception.Message.ShouldContain("web.config");
    }

    [Fact]
    public void A_Remove_Of_An_Absent_Key_Is_Tolerated_As_On_Iis()
    {
        var app = WriteConfig(
            "web.config",
            "<staticContent><remove fileExtension=\".notthere\" /></staticContent>");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.ServesStaticContent(".css").ShouldBeTrue();
    }

    [Fact]
    public void Clear_Empties_The_Inherited_Collection()
    {
        var app = WriteConfig(
            "web.config",
            "<staticContent><clear />"
            + "<mimeMap fileExtension=\".only\" mimeType=\"application/x-only\" /></staticContent>");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.ServesStaticContent(".css").ShouldBeFalse();
        configuration.ServesStaticContent(".only").ShouldBeTrue();
    }

    [Fact]
    public void A_MimeMap_Missing_Its_Type_Fails_Naming_The_Attribute()
    {
        var app = WriteConfig(
            "web.config",
            "<staticContent><mimeMap fileExtension=\".x\" /></staticContent>");

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("mimeType");
    }

    [Fact]
    public void An_Absent_Application_Config_Yields_The_Baseline()
    {
        var configuration = IisServerConfiguration.Load(
            Baseline(), Path.Combine(_root.FullName, "missing", "web.config"));

        configuration.ServesStaticContent(".js").ShouldBeTrue();
    }

    [Fact]
    public void A_Missing_Baseline_Fails_Naming_The_Path()
    {
        Should.Throw<FileNotFoundException>(
                () => IisServerConfiguration.Load(
                    Path.Combine(_root.FullName, "gone.config"), Baseline()))
            .Message.ShouldContain("gone.config");
    }
}
