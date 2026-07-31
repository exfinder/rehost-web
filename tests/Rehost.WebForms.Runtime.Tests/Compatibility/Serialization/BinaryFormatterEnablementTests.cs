using System.Text.Json;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Serialization;

public sealed class BinaryFormatterEnablementTests
{
    private const string Switch = "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization";

    // The runtime reads this switch once and latches it, so a host that touched BinaryFormatter
    // before the module initializer ran would be stuck with the SDK's false. The shipped targets
    // put it in the consuming application's runtimeconfig instead.
    [Fact]
    public void The_Host_Runtime_Configuration_Enables_Binary_Formatter_Serialization()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Rehost.WebForms.Runtime.Tests.runtimeconfig.json");

        File.Exists(path).ShouldBeTrue(path);

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        document.RootElement
            .GetProperty("runtimeOptions")
            .GetProperty("configProperties")
            .GetProperty(Switch)
            .GetBoolean()
            .ShouldBeTrue();
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
