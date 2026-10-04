using System.Configuration;
using System.Web.Util;
using Rehost.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.Hosting;

[Collection(nameof(ApplicationBootstrapCollection))]
public sealed class ProbingPathsTests
{
    private const string Root = "/app";

    private const string ConfigPath = "/app/web.config";

    [Fact]
    public void Reads_Each_Folder_Under_The_Root_In_Written_Order()
    {
        var directories = ProbingPaths.Parse(
            """
            <runtime>
              <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                <probing privatePath="bin; bin\probing ;bin/" />
              </assemblyBinding>
            </runtime>
            """,
            Root,
            ConfigPath);

        directories.ShouldBe(
        [
            Path.GetFullPath(Path.Combine(Root, "bin")),
            Path.GetFullPath(Path.Combine(Root, "bin", "probing")),
        ]);
    }

    [Fact]
    public void A_Rooted_Entry_Fails_Naming_The_File_And_The_Value()
    {
        var exception = Should.Throw<ConfigurationErrorsException>(() => ProbingPaths.Parse(
            """
            <runtime>
              <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                <probing privatePath="bin;/opt/providers" />
              </assemblyBinding>
            </runtime>
            """,
            Root,
            ConfigPath));

        exception.Message.ShouldContain($"'{ConfigPath}'", Case.Sensitive);
        exception.Message.ShouldContain("'/opt/providers'", Case.Sensitive);
        exception.Message.ShouldContain("relative to the application root", Case.Sensitive);
    }

    [Fact]
    public void An_Entry_Escaping_The_Root_Fails()
    {
        var exception = Should.Throw<ConfigurationErrorsException>(() => ProbingPaths.Parse(
            """
            <runtime>
              <assemblyBinding>
                <probing privatePath="bin\..\..\shared" />
              </assemblyBinding>
            </runtime>
            """,
            Root,
            ConfigPath));

        exception.Message.ShouldContain(@"'bin\..\..\shared'", Case.Sensitive);
    }

    [Fact]
    public void Binding_Redirects_Alone_Probe_Nothing_And_Activate()
    {
        using var application = TemporaryApplication.Create();
        File.WriteAllText(
            Path.Combine(application.PhysicalRoot.FullName, "web.config"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <runtime>
                <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                  <dependentAssembly>
                    <assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" />
                    <bindingRedirect oldVersion="0.0.0.0-13.0.0.0" newVersion="13.0.0.0" />
                  </dependentAssembly>
                </assemblyBinding>
              </runtime>
              <system.web>
                <compilation targetFramework="4.8" />
              </system.web>
            </configuration>
            """);

        ApplicationConfigurationPreflight.Validate(application.CreateConfiguration()).ShouldBeEmpty();
    }
}
