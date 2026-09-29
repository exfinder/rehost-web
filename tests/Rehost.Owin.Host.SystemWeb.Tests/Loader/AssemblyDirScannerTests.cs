using System.Reflection;
using global::Owin.Loader;
using Shouldly;
using Xunit;

namespace Rehost.Owin.Host.SystemWeb.Tests.Loader;

public sealed class AssemblyDirScannerTests
{
    // The upstream scanner probed AppDomainSetup.PrivateBinPath, which modern .NET does not
    // carry; this pins that the replacement still reaches assemblies beside the entry point.
    [Fact]
    public void ScanningTheBaseDirectoryFindsAStartupAttributeInAnAssemblyBesideTheBinary()
    {
        var errors = new List<string>();

        var startup = new DefaultLoader((IEnumerable<Assembly>?)null)
            .Load(ScannedStartup.FriendlyName, errors);

        startup.ShouldNotBeNull(string.Join("; ", errors));

        var builder = new StubAppBuilder();
        startup(builder);

        builder.Properties[ScannedStartup.RanKey].ShouldBe(true);
    }
}
