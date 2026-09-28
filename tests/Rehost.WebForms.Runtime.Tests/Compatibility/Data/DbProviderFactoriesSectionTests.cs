using System.Configuration;
using System.Data.Common;
using Rehost.WebForms.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Data;

[Collection(nameof(ApplicationBootstrapCollection))]
public sealed class DbProviderFactoriesSectionTests : IDisposable
{
    private const string SqlClient = "System.Data.SqlClient";
    private static readonly string ConfiguredType = typeof(ConfiguredFactory).AssemblyQualifiedName!;

    private readonly List<string> _names = [];

    [Fact]
    public void A_Web_Config_Row_Registers_Its_Factory()
    {
        var name = NewName();
        using var application = WithWebConfig($"""
            <system.data>
              <DbProviderFactories>
                <remove invariant="{name}" />
                <add name="Configured" description="Configured provider" invariant="{name}" type="{ConfiguredType}" />
              </DbProviderFactories>
            </system.data>
            """);

        DbProviderFactoriesSection.Register(application.CreateConfiguration().OpenMappedConfiguration());

        DbProviderFactories.GetFactory(name).ShouldBeSameAs(ConfiguredFactory.Instance);
    }

    [Fact]
    public void SqlClient_Is_Built_In_And_A_Config_Row_Does_Not_Replace_It()
    {
        DbProviderFactories.UnregisterFactory(SqlClient);
        using var application = WithWebConfig($"""
            <system.data>
              <DbProviderFactories>
                <add name="Override" description="Override" invariant="{SqlClient}" type="{ConfiguredType}" />
              </DbProviderFactories>
            </system.data>
            """);

        DbProviderFactoriesSection.Register(application.CreateConfiguration().OpenMappedConfiguration());

        DbProviderFactories.GetFactory(SqlClient).GetType().FullName.ShouldBe("System.Data.SqlClient.SqlClientFactory");
    }

    [Fact]
    public void A_Name_The_Host_Registered_Keeps_The_Host_Factory()
    {
        var name = NewName();
        DbProviderFactories.RegisterFactory(name, HostFactory.Instance);
        using var application = WithWebConfig($"""
            <system.data>
              <DbProviderFactories>
                <add name="Configured" description="Configured provider" invariant="{name}" type="{ConfiguredType}" />
              </DbProviderFactories>
            </system.data>
            """);

        DbProviderFactoriesSection.Register(application.CreateConfiguration().OpenMappedConfiguration());

        DbProviderFactories.GetFactory(name).ShouldBeSameAs(HostFactory.Instance);
    }

    [Theory]
    [InlineData("""name="Configured" invariant="Broken.Row" type="T, A" """, "description")]
    [InlineData("""name="" description="Broken" invariant="Broken.Row" type="T, A" """, "name")]
    public void A_Broken_Row_Fails_Preflight_At_Its_Line(string attributes, string attribute)
    {
        using var application = WithWebConfig($"""
            <system.data>
              <DbProviderFactories>
                <add {attributes}/>
              </DbProviderFactories>
            </system.data>
            """);

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => ApplicationConfigurationPreflight.Validate(application.CreateConfiguration()));

        exception.Message.ShouldContain($"'{attribute}'", Case.Sensitive);
        exception.Filename.ShouldEndWith("web.config");
        exception.Line.ShouldBe(5);
    }

    public void Dispose()
    {
        foreach (var name in _names)
        {
            DbProviderFactories.UnregisterFactory(name);
        }
    }

    private string NewName()
    {
        var name = $"Rehost.Tests.{Guid.NewGuid():N}";
        _names.Add(name);
        return name;
    }

    private static TemporaryApplication WithWebConfig(string sections)
    {
        var application = TemporaryApplication.Create();
        File.WriteAllText(
            Path.Combine(application.PhysicalRoot.FullName, "web.config"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
            {sections}
              <system.web>
                <compilation targetFramework="4.8" />
              </system.web>
            </configuration>
            """);
        return application;
    }

    public sealed class ConfiguredFactory : DbProviderFactory
    {
        public static readonly ConfiguredFactory Instance = new();
    }

    public sealed class HostFactory : DbProviderFactory
    {
        public static readonly HostFactory Instance = new();
    }
}
