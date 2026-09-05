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
            """<?xml version="1.0"?><configuration><system.webServer>"""
            + systemWebServerContent
            + "</system.webServer></configuration>");
        return path;
    }

    private string Baseline() => WriteConfig(
        "baseline.config",
        """
        <staticContent>
          <mimeMap fileExtension=".css" mimeType="text/css" />
          <mimeMap fileExtension=".js" mimeType="application/javascript" />
        </staticContent>
        <security>
          <requestFiltering>
            <hiddenSegments>
              <add segment="App_Data" />
            </hiddenSegments>
          </requestFiltering>
        </security>
        """);

    private string BaselineWithFileExtensions() => WriteConfig(
        "baseline-fx.config",
        """
        <security>
          <requestFiltering>
            <fileExtensions allowUnlisted="true">
              <add fileExtension=".cs" allowed="false" />
              <add fileExtension=".config" allowed="false" />
            </fileExtensions>
          </requestFiltering>
        </security>
        """);

    [Fact]
    public void Amendments_Merge_Over_The_Baseline()
    {
        var app = WriteConfig(
            "web.config",
            """
            <staticContent>
              <remove fileExtension=".css" />
              <mimeMap fileExtension=".probe" mimeType="application/x-probe" />
            </staticContent>
            <security>
              <requestFiltering>
                <hiddenSegments>
                  <remove segment="App_Data" />
                  <add segment="Private" />
                </hiddenSegments>
              </requestFiltering>
            </security>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.ServesStaticContent(".css").ShouldBeFalse();
        configuration.ServesStaticContent(".probe").ShouldBeTrue();
        configuration.StaticContentTypeOf(".PROBE").ShouldBe("application/x-probe");
        configuration.StaticContentTypeOf(".js").ShouldBe("application/javascript");
        configuration.IsHiddenSegment("App_Data").ShouldBeFalse();
        configuration.IsHiddenSegment("private").ShouldBeTrue();
    }

    // MH29: allowUnlisted="false" turns the deny list into an allow list, and IIS refused an
    // extensionless path under it as readily as a named extension.
    [Fact]
    public void An_Allow_List_Refuses_Every_Extension_It_Does_Not_Name()
    {
        var app = WriteConfig(
            "web.config",
            """
            <security>
              <requestFiltering>
                <fileExtensions allowUnlisted="false">
                  <add fileExtension=".txt" allowed="true" />
                </fileExtensions>
              </requestFiltering>
            </security>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.IsForbiddenExtension(".txt").ShouldBeFalse();
        configuration.IsForbiddenExtension(".TXT").ShouldBeFalse();
        configuration.IsForbiddenExtension(".dat").ShouldBeTrue();
        configuration.IsForbiddenExtension(".aspx").ShouldBeTrue();
        configuration.IsForbiddenExtension("").ShouldBeTrue();
        configuration.IsForbiddenExtension(null).ShouldBeTrue();
    }

    // The baseline ships allowUnlisted="true", so an application that only adds rows keeps the
    // deny-list reading: everything it does not name stays servable.
    [Fact]
    public void A_Deny_List_Refuses_Only_What_It_Names()
    {
        var app = WriteConfig(
            "web.config",
            """
            <security>
              <requestFiltering>
                <fileExtensions>
                  <add fileExtension=".dat" allowed="false" />
                </fileExtensions>
              </requestFiltering>
            </security>
            """);

        var configuration = IisServerConfiguration.Load(BaselineWithFileExtensions(), app);

        configuration.IsForbiddenExtension(".dat").ShouldBeTrue();
        configuration.IsForbiddenExtension(".cs").ShouldBeTrue();
        configuration.IsForbiddenExtension(".txt").ShouldBeFalse();
        configuration.IsForbiddenExtension("").ShouldBeFalse();
    }

    // MH30: an application removes an inherited deny row through the ordinary collection
    // semantics. The staticContent MIME gate is what still stops the file being served.
    [Fact]
    public void An_Application_Removes_An_Inherited_Deny_Row()
    {
        var app = WriteConfig(
            "web.config",
            """
            <security>
              <requestFiltering>
                <fileExtensions><remove fileExtension=".cs" /></fileExtensions>
              </requestFiltering>
            </security>
            """);

        var configuration = IisServerConfiguration.Load(BaselineWithFileExtensions(), app);

        configuration.IsForbiddenExtension(".cs").ShouldBeFalse();
        configuration.ServesStaticContent(".cs").ShouldBeFalse();
    }

    // MH31: IIS types allowed as bool and refuses a typo with a 500.19 naming the attribute, so
    // an unparsable value must not read as "not false" and quietly serve the file.
    [Fact]
    public void A_Non_Boolean_Allowed_Value_Fails_Naming_The_File()
    {
        var app = WriteConfig(
            "web.config",
            """
            <security>
              <requestFiltering>
                <fileExtensions><add fileExtension=".dat" allowed="flase" /></fileExtensions>
              </requestFiltering>
            </security>
            """);

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("allowed=\"flase\"");
        exception.Message.ShouldContain(".dat");
        exception.Message.ShouldContain(app);
    }

    [Fact]
    public void A_Non_Boolean_AllowUnlisted_Value_Fails_Naming_The_File()
    {
        var app = WriteConfig(
            "web.config",
            """
            <security>
              <requestFiltering>
                <fileExtensions allowUnlisted="yes" />
              </requestFiltering>
            </security>
            """);

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("allowUnlisted=\"yes\"", Case.Sensitive);
        exception.Message.ShouldContain(app);
    }

    [Fact]
    public void A_Duplicate_Add_Fails_Naming_The_File_As_IIS_Refuses_It()
    {
        var app = WriteConfig(
            "web.config",
            """<staticContent><mimeMap fileExtension=".css" mimeType="text/css" /></staticContent>""");

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
            """<staticContent><remove fileExtension=".notthere" /></staticContent>""");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.ServesStaticContent(".css").ShouldBeTrue();
    }

    [Fact]
    public void Clear_Empties_The_Inherited_Collection()
    {
        var app = WriteConfig(
            "web.config",
            """
            <staticContent>
              <clear />
              <mimeMap fileExtension=".only" mimeType="application/x-only" />
            </staticContent>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.ServesStaticContent(".css").ShouldBeFalse();
        configuration.ServesStaticContent(".only").ShouldBeTrue();
    }

    [Fact]
    public void A_MimeMap_Missing_Its_Type_Fails_Naming_The_Attribute()
    {
        var app = WriteConfig(
            "web.config",
            """<staticContent><mimeMap fileExtension=".x" /></staticContent>""");

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("mimeType", Case.Sensitive);
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
