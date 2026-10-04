using System.Configuration;
using System.Web.Util;
using Rehost.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.Hosting;

[Collection(nameof(ApplicationBootstrapCollection))]
public sealed class ProbingPathsTests
{
    [Fact]
    public void Reads_Each_Folder_Under_The_Root_In_Written_Order()
    {
        using var application = TemporaryApplication.Create();
        WriteWebConfig(application, """
            <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
              <probing privatePath="bin; bin\probing ;" />
            </assemblyBinding>
            """);

        var directories = ProbingPaths.Read(application.PhysicalRoot.FullName);

        directories.ShouldBe(
        [
            Path.Combine(application.PhysicalRoot.FullName, "bin"),
            Path.Combine(application.PhysicalRoot.FullName, "bin", "probing"),
        ]);
    }

    [Fact]
    public void A_Rooted_Entry_Fails_Naming_The_Value()
    {
        using var application = TemporaryApplication.Create();
        WriteWebConfig(application, """
            <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
              <probing privatePath="bin;/opt/providers" />
            </assemblyBinding>
            """);

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => ProbingPaths.Read(application.PhysicalRoot.FullName));

        exception.Message.ShouldContain("'/opt/providers'", Case.Sensitive);
        exception.Message.ShouldContain("relative to the application root", Case.Sensitive);
    }

    [Fact]
    public void An_Entry_Escaping_The_Root_Fails()
    {
        using var application = TemporaryApplication.Create();
        WriteWebConfig(application, """
            <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
              <probing privatePath="bin\..\..\shared" />
            </assemblyBinding>
            """);

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => ProbingPaths.Read(application.PhysicalRoot.FullName));

        exception.Message.ShouldContain(@"'bin\..\..\shared'", Case.Sensitive);
    }

    [Fact]
    public void Binding_Redirects_Alone_Probe_Nothing_And_Activate()
    {
        using var application = TemporaryApplication.Create();
        WriteWebConfig(application, """
            <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
              <dependentAssembly>
                <assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" />
                <bindingRedirect oldVersion="0.0.0.0-13.0.0.0" newVersion="13.0.0.0" />
              </dependentAssembly>
            </assemblyBinding>
            """);

        ProbingPaths.Read(application.PhysicalRoot.FullName).ShouldBeEmpty();
        Should.NotThrow(() => ApplicationConfigurationPreflight.Validate(application.CreateConfiguration()));
    }

    private static void WriteWebConfig(TemporaryApplication application, string assemblyBinding)
    {
        File.WriteAllText(
            Path.Combine(application.PhysicalRoot.FullName, "web.config"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <runtime>
                {assemblyBinding}
              </runtime>
              <system.web>
                <compilation targetFramework="4.8" />
              </system.web>
            </configuration>
            """);
    }
}
