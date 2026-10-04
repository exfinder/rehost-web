using System.Configuration;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.IisConfig;

// What a <caching><profiles> row puts on a static response (CP1-CP13, CP45): location decides
// the word, policy decides whether there is one, and the section's own enabled attribute does
// not reach the wire.
public sealed class CachingProfilesTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-caching-");

    public void Dispose() => _root.Delete(recursive: true);

    [Theory]
    [InlineData("""location="Client" """, "private")]
    [InlineData("""location="ServerAndClient" """, "private")]
    [InlineData("""location="Any" """, "public")]
    [InlineData("""location="Downstream" """, "public")]
    [InlineData("""location="Server" """, "no-cache")]
    [InlineData("""location="None" """, "no-cache")]
    [InlineData("", "no-cache")]
    public void Location_Decides_The_Word(string location, string word)
    {
        var app = WriteConfig(
            "web.config",
            $"""
            <caching>
              <profiles>
                <add extension=".css" policy="CacheForTimePeriod" duration="00:10:00"
                     varyByHeaders="Browser" varyByQueryString="id" {location}/>
              </profiles>
            </caching>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.StaticCacheControl(null, ".css").ShouldBe(word);
        configuration.StaticCacheControl(null, ".CSS").ShouldBe(word);
        configuration.StaticCacheControl(null, ".js").ShouldBeNull();
        configuration.StaticCacheControl(null, null).ShouldBeNull();
        configuration.UserModeCachedExtensions.ShouldBe([".css"]);
    }

    // CP4: a kernel-mode policy on its own adds nothing to the wire, and it is not the stored
    // copy the preflight reports either.
    [Theory]
    [InlineData("""policy="DontCache" """)]
    [InlineData("""policy="DisableCache" """)]
    [InlineData("""kernelCachePolicy="CacheUntilChange" """)]
    public void A_Policy_That_Stores_No_User_Mode_Copy_Sends_Nothing(string policy)
    {
        var app = WriteConfig(
            "web.config",
            $"""
            <caching>
              <profiles>
                <add extension=".css" {policy} location="Client" />
              </profiles>
            </caching>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.StaticCacheControl(null, ".css").ShouldBeNull();
        configuration.UserModeCachedExtensions.ShouldBeEmpty();
    }

    // CP45: enabled="false" stopped IIS storing anything, and the header word still applied.
    // The amendment rides the same merge as staticContent, and the reported list is sorted.
    [Fact]
    public void A_Disabled_Section_Still_Merges_Its_Words_Over_The_Baseline()
    {
        var app = WriteConfig(
            "web.config",
            """
            <caching enabled="false" enableKernelCache="false">
              <profiles>
                <remove extension=".gif" />
                <add extension=".css" policy="CacheUntilChange" location="Any" />
              </profiles>
            </caching>
            """);

        var configuration = IisServerConfiguration.Load(BaselineWithProfiles(), app);

        configuration.StaticCacheControl(null, ".gif").ShouldBeNull();
        configuration.StaticCacheControl(null, ".svg").ShouldBe("private");
        configuration.StaticCacheControl(null, ".css").ShouldBe("public");
        configuration.UserModeCachedExtensions.ShouldBe([".css", ".svg"]);
    }

    [Fact]
    public void An_Unknown_Enum_Value_Names_Its_Attribute()
    {
        var app = WriteConfig(
            "web.config",
            """
            <caching>
              <profiles>
                <add extension=".css" policy="CacheUntilChange" location="Proxy" />
              </profiles>
            </caching>
            """);

        var failure = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        failure.Message.ShouldStartWith(
            """<add extension=".css" location="Proxy">""", Case.Sensitive);
        failure.Message.ShouldContain(app, Case.Sensitive);
    }

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
        </staticContent>
        """);

    private string BaselineWithProfiles() => WriteConfig(
        "baseline-profiles.config",
        """
        <caching>
          <profiles>
            <add extension=".gif" policy="CacheUntilChange" location="Server" />
            <add extension=".svg" policy="CacheUntilChange" location="Client" />
          </profiles>
        </caching>
        """);
}
