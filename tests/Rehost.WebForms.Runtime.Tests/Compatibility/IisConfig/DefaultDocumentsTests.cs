using System.Configuration;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The measured <defaultDocument> semantics (readings D8-D13): ordered list, app adds prepend,
// and a broken section deferring its failure to the accessor instead of activation.
public sealed class DefaultDocumentsTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-defdoc-");

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
        "<defaultDocument enabled=\"true\"><files>"
        + "<add value=\"Default.htm\" />"
        + "<add value=\"index.html\" />"
        + "<add value=\"default.aspx\" />"
        + "</files></defaultDocument>");

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
            "<defaultDocument><files><add value=\"custom.htm\" /></files></defaultDocument>");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.DefaultDocuments.Files.ShouldBe(
            new[] { "custom.htm", "Default.htm", "index.html", "default.aspx" });
    }

    [Fact]
    public void Remove_And_Clear_Reach_Inherited_Entries()
    {
        var removed = WriteConfig(
            "removed.config",
            "<defaultDocument><files><remove value=\"INDEX.HTML\" /></files></defaultDocument>");
        var cleared = WriteConfig(
            "cleared.config",
            "<defaultDocument><files><clear /><add value=\"only.htm\" /></files></defaultDocument>");

        IisServerConfiguration.Load(Baseline(), removed)
            .DefaultDocuments.Files.ShouldBe(new[] { "Default.htm", "default.aspx" });
        IisServerConfiguration.Load(Baseline(), cleared)
            .DefaultDocuments.Files.ShouldBe(new[] { "only.htm" });
    }

    [Fact]
    public void Disabling_Keeps_The_Inherited_List_Intact()
    {
        var app = WriteConfig("web.config", "<defaultDocument enabled=\"false\" />");

        var documents = IisServerConfiguration.Load(Baseline(), app).DefaultDocuments;

        documents.Enabled.ShouldBeFalse();
        documents.Files.Count.ShouldBe(3);
    }

    [Fact]
    public void A_Duplicate_Add_Defers_Its_Failure_To_The_Accessor()
    {
        var app = WriteConfig(
            "web.config",
            "<defaultDocument><files><add value=\"DEFAULT.HTM\" /></files></defaultDocument>");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => configuration.DefaultDocuments);
        exception.Message.ShouldContain("DEFAULT.HTM");
        exception.Message.ShouldContain("web.config");
    }

    [Fact]
    public void A_Broken_Section_Leaves_The_Other_Tenants_Serving()
    {
        var app = WriteConfig(
            "web.config",
            "<staticContent><mimeMap fileExtension=\".probe\" mimeType=\"application/x-probe\" /></staticContent>"
            + "<defaultDocument><files><add value=\"Default.htm\" /></files></defaultDocument>");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.ServesStaticContent(".probe").ShouldBeTrue();
        Should.Throw<ConfigurationErrorsException>(() => configuration.DefaultDocuments);
    }

    [Fact]
    public void A_Missing_Value_Attribute_Defers_Naming_The_Attribute()
    {
        var app = WriteConfig(
            "web.config", "<defaultDocument><files><add /></files></defaultDocument>");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        Should.Throw<ConfigurationErrorsException>(() => configuration.DefaultDocuments)
            .Message.ShouldContain("value");
    }
}
