using System.Text.Json;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Serialization;

public sealed class BinaryFormatterEnablementTests
{
    private const string Switch = "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization";

    // BinaryFormatter caches the switch on first read, so an application that serialized before
    // it first touched System.Web would keep the SDK's false whatever the module initializer did
    // afterwards. Every executable consuming the port imports the shipped targets, which write
    // the switch into its own runtimeconfig. Dropping that import is silent without this.
    [Theory]
    [InlineData("Rehost.WebForms.Runtime.Tests")]
    [InlineData("Rehost.WebForms.ScenarioHost")]
    public void A_Consuming_Application_Enables_Binary_Formatter_Serialization(string application)
    {
        var path = Path.Combine(
            RepositoryRoot,
            "tests",
            application,
            "bin",
            "Debug",
            "net10.0",
            application + ".runtimeconfig.json");

        File.Exists(path).ShouldBeTrue(path);

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        document.RootElement
            .GetProperty("runtimeOptions")
            .GetProperty("configProperties")
            .GetProperty(Switch)
            .GetBoolean()
            .ShouldBeTrue(path);
    }

    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null
            && !File.Exists(Path.Combine(directory.FullName, "Rehost.WebForms.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root was not found.");
    }

    // The out-of-band package must win over the shared framework's throwing stub.
    [Fact]
    public void Binary_Formatter_Resolves_To_The_Out_Of_Band_Implementation()
    {
#pragma warning disable SYSLIB0011
        var assembly = typeof(System.Runtime.Serialization.Formatters.Binary.BinaryFormatter).Assembly;
#pragma warning restore SYSLIB0011

        assembly.GetName().Name.ShouldBe("System.Runtime.Serialization.Formatters");
        assembly.GetName().Version.ShouldBe(new Version(10, 0, 0, 0));
    }
}
