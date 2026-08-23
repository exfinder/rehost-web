using System.Configuration;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The measured <defaultDocument> semantics (readings D8-D12): ordered list with app adds
// prepending. A broken section fails activation, as every honored section does (P60).
public sealed class DefaultDocumentsTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-defdoc-");

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
        <defaultDocument enabled="true">
          <files>
            <add value="Default.htm" />
            <add value="index.html" />
            <add value="default.aspx" />
          </files>
        </defaultDocument>
        """);

    [Fact]
    public void The_Baseline_List_Keeps_Its_Order()
    {
        var configuration = IisServerConfiguration.Load(
            Baseline(), Path.Combine(_root.FullName, "missing", "web.config"));

        configuration.DefaultDocuments.Enabled.ShouldBeTrue();
        configuration.DefaultDocuments.Files.ShouldBe(
            new[] { "Default.htm", "index.html", "default.aspx" });
    }

    [Fact]
    public void An_App_Add_Prepends_Over_The_Inherited_List()
    {
        var app = WriteConfig(
            "web.config",
            """<defaultDocument><files><add value="custom.htm" /></files></defaultDocument>""");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.DefaultDocuments.Files.ShouldBe(
            new[] { "custom.htm", "Default.htm", "index.html", "default.aspx" });
    }

    [Fact]
    public void Remove_And_Clear_Reach_Inherited_Entries()
    {
        var removed = WriteConfig(
            "removed.config",
            """<defaultDocument><files><remove value="INDEX.HTML" /></files></defaultDocument>""");
        var cleared = WriteConfig(
            "cleared.config",
            """<defaultDocument><files><clear /><add value="only.htm" /></files></defaultDocument>""");

        IisServerConfiguration.Load(Baseline(), removed)
            .DefaultDocuments.Files.ShouldBe(new[] { "Default.htm", "default.aspx" });
        IisServerConfiguration.Load(Baseline(), cleared)
            .DefaultDocuments.Files.ShouldBe(new[] { "only.htm" });
    }

    [Fact]
    public void Disabling_Keeps_The_Inherited_List_Intact()
    {
        var app = WriteConfig("web.config", """<defaultDocument enabled="false" />""");

        var documents = IisServerConfiguration.Load(Baseline(), app).DefaultDocuments;

        documents.Enabled.ShouldBeFalse();
        documents.Files.Count.ShouldBe(3);
    }

    [Fact]
    public void A_Duplicate_Add_Fails_Activation_Naming_The_File()
    {
        var app = WriteConfig(
            "web.config",
            """<defaultDocument><files><add value="DEFAULT.HTM" /></files></defaultDocument>""");

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("DEFAULT.HTM");
        exception.Message.ShouldContain("web.config");
    }

    [Fact]
    public void A_Missing_Value_Attribute_Fails_Activation_Naming_The_Attribute()
    {
        var app = WriteConfig(
            "web.config", "<defaultDocument><files><add /></files></defaultDocument>");

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("value");
    }

    [Fact]
    public void A_Non_Boolean_Enabled_Fails_Activation_Naming_The_Value()
    {
        var app = WriteConfig("web.config", """<defaultDocument enabled="yes" />""");

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("yes");
    }
}
