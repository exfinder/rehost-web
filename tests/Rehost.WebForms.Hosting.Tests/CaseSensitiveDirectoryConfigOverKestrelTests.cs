using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Configuration paths are lowercased before they are mapped, and Visual Studio names the file
// Web.config, so NTFS folded both back onto what is on disk. On a case-sensitive filesystem the
// directory misses at the configuration system's own map-path seam and the file misses at the
// composed name, and the subtree's rules — here an authorization deny — vanish (ledger P70).
public sealed class CaseSensitiveDirectoryConfigOverKestrelTests(CaseSensitiveLiveScenario scenario)
{
    [Fact]
    public async Task A_Directory_Web_Config_Denies_The_Request()
    {
        var live = scenario.RequireLive();

        var response = await live.Client.GetAsync("/Guarded/Secret.aspx");

        response.StatusCode.ShouldBe(401);
        response.Text.ShouldNotContain("secret-body");
    }

    [Fact]
    public async Task A_ConfigSource_Spelled_In_Another_Casing_Denies_The_Request()
    {
        var live = scenario.RequireLive();

        var response = await live.Client.GetAsync("/Sourced/Secret.aspx");

        response.StatusCode.ShouldBe(401);
        response.Text.ShouldNotContain("sourced-secret-body");
    }
}
