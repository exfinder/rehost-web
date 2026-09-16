using System.Configuration;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The section as an application receives it: IIS's ten inherited rows from the shipped baseline,
// then the application's own modes and rows on top. The refusals are the shapes IIS locked or
// answered with a 500.19, plus the response mode this port cannot run.
public sealed class HttpErrorsTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-errors-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void An_Application_Without_The_Section_Inherits_The_Baseline_Rows_And_Modes()
    {
        var errors = Load("");

        errors.ErrorMode.ShouldBe(HttpErrorMode.DetailedLocalOnly);
        errors.ExistingResponse.ShouldBe(ExistingResponse.Auto);
        errors.Rows.Select(row => row.Status)
            .ShouldBe([401, 403, 404, 405, 406, 412, 431, 500, 501, 502]);
        errors.Rows.ShouldAllBe(row => row.Mode == HttpErrorRowMode.BuiltIn);
        errors.Rows.ShouldAllBe(row => row.SubStatus == -1);
        errors.Rows.ShouldAllBe(row => row.PhysicalPath == null);
        errors.DetailedFor(isLocal: true).ShouldBeTrue();
        errors.DetailedFor(isLocal: false).ShouldBeFalse();
    }

    [Fact]
    public void An_Application_Replaces_A_Row_With_A_File_Whose_Path_And_Type_Resolve()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "he"));
        var page = Path.Combine(_root.FullName, "he", "err404.json");
        File.WriteAllText(page, """{"error":404}""");

        var errors = Load(
            """
            <httpErrors errorMode="Custom" existingResponse="Replace">
              <remove statusCode="404" subStatusCode="-1" />
              <error statusCode="404" path="he/err404.json" />
              <error statusCode="404" subStatusCode="8" path="/filtered" responseMode="Redirect" />
            </httpErrors>
            """);

        errors.ErrorMode.ShouldBe(HttpErrorMode.Custom);
        errors.ExistingResponse.ShouldBe(ExistingResponse.Replace);
        errors.DetailedFor(isLocal: true).ShouldBeFalse();

        var file = errors.Find(404, 0).ShouldNotBeNull();
        file.Mode.ShouldBe(HttpErrorRowMode.File);
        file.PhysicalPath.ShouldBe(page);
        file.ContentType.ShouldBe("application/json");

        var exact = errors.Find(404, 8).ShouldNotBeNull();
        exact.Mode.ShouldBe(HttpErrorRowMode.Redirect);
        exact.Location.ShouldBe("/filtered");
        errors.Find(404, 9).ShouldBe(file);
        errors.Find(410, 0).ShouldBeNull();
    }

    [Theory]
    [InlineData("410")]
    [InlineData("404")]
    public void A_Duplicate_Key_Fails_Activation(string status)
    {
        var failure = Refusal(
            $"""
            <httpErrors>
              <error statusCode="410" path="one.htm" />
              <error statusCode="{status}" path="two.htm" />
            </httpErrors>
            """);

        failure.ShouldContain($"""<error statusCode="{status}">""", Case.Sensitive);
        failure.ShouldContain("duplicates an entry");
    }

    [Theory]
    [InlineData(
        """<httpErrors><error statusCode="410" path="/err.aspx" responseMode="executeUrl" /></httpErrors>""",
        """<error statusCode="410" responseMode="ExecuteURL">""",
        "second managed request")]
    [InlineData(
        """<httpErrors defaultResponseMode="ExecuteURL"><error statusCode="410" path="/err.aspx" /></httpErrors>""",
        """<error statusCode="410"> in '""",
        """defaultResponseMode="ExecuteURL", which""")]
    [InlineData(
        """<httpErrors defaultPath="def.htm" />""",
        """<httpErrors defaultPath="def.htm">""",
        "locked below the server")]
    [InlineData(
        """<httpErrors allowAbsolutePathsWhenDelegated="true" />""",
        """<httpErrors allowAbsolutePathsWhenDelegated="true">""",
        "locked below the server")]
    [InlineData(
        """<httpErrors errorMode="Verbose" />""",
        """<httpErrors errorMode="Verbose">""",
        "is not one of DetailedLocalOnly, Custom, Detailed")]
    [InlineData(
        """<httpErrors><error statusCode="410" path="/srv/err.htm" /></httpErrors>""",
        """<error statusCode="410" path="/srv/err.htm">""",
        "is an absolute path")]
    public void A_Shape_IIS_Locked_Or_This_Port_Cannot_Run_Fails_Activation(
        string section, string element, string rule)
    {
        var failure = Refusal(section);

        failure.ShouldContain(element, Case.Sensitive);
        failure.ShouldContain(rule, Case.Sensitive);
    }

    [Fact]
    public void A_File_Row_Leaving_The_Application_Root_Fails_Activation()
    {
        var failure = Refusal(
            """
            <httpErrors>
              <remove statusCode="404" />
              <error statusCode="404" path="../outside.htm" />
            </httpErrors>
            """);

        failure.ShouldContain("""<error statusCode="404" path="../outside.htm">""", Case.Sensitive);
        failure.ShouldContain("outside the application root");
    }

    [Fact]
    public void A_Redirect_Row_That_Is_Neither_Site_Absolute_Nor_A_Url_Fails_Activation()
    {
        var failure = Refusal(
            """
            <httpErrors>
              <error statusCode="410" path="err.htm" responseMode="Redirect" />
            </httpErrors>
            """);

        failure.ShouldContain("""<error statusCode="410" path="err.htm">""", Case.Sensitive);
        failure.ShouldContain("site-absolute path nor an absolute URL");
    }

    private static string ShippedBaseline => Path.Combine(
        AppContext.BaseDirectory, "configs", "rehost-webforms.applicationHost.config");

    private HttpErrors Load(string systemWebServerContent) =>
        IisServerConfiguration.Load(ShippedBaseline, WriteApplication(systemWebServerContent))
            .HttpErrors;

    private string Refusal(string systemWebServerContent)
    {
        var application = WriteApplication(systemWebServerContent);
        var failure = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(ShippedBaseline, application));

        failure.Message.ShouldContain(application);
        return failure.Message;
    }

    private string WriteApplication(string systemWebServerContent) => WriteConfig(
        $"""
        <system.webServer>
        {systemWebServerContent}
        </system.webServer>
        """);

    private string WriteConfig(string configurationContent)
    {
        var path = Path.Combine(_root.FullName, "web.config");
        File.WriteAllText(
            path,
            $"""
            <?xml version="1.0"?>
            <configuration>
            {configurationContent}
            </configuration>
            """);
        return path;
    }
}
