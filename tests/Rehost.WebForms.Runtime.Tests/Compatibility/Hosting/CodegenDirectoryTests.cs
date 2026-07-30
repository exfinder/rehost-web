using System.Configuration;
using System.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

public sealed class CodegenDirectoryTests
{
    [Fact]
    public void Host_supplied_root_wins_over_configured_temp_directory()
    {
        var root = CodegenDirectory.SelectTempRoot(
            hostSupplied: Path.Combine(Path.GetTempPath(), "host-root"),
            configured: Path.Combine(Path.GetTempPath(), "configured-root"),
            attributeName: "tempDirectory",
            fileName: "web.config",
            lineNumber: 11);

        root.ShouldBe(Path.Combine(Path.GetTempPath(), "host-root"));
    }

    [Fact]
    public void Configured_temp_directory_is_used_when_no_host_root_exists()
    {
        var configured = Path.Combine(Path.GetTempPath(), "configured-root");

        var root = CodegenDirectory.SelectTempRoot(
            hostSupplied: null,
            configured: configured + Path.DirectorySeparatorChar,
            attributeName: "tempDirectory",
            fileName: "web.config",
            lineNumber: 11);

        root.ShouldBe(configured);
    }

    [Fact]
    public void Absent_root_falls_back_to_the_portable_default()
    {
        var root = CodegenDirectory.SelectTempRoot(
            hostSupplied: null,
            configured: null,
            attributeName: null,
            fileName: null,
            lineNumber: 0);

        root.ShouldBe(Path.Combine(
            Path.TrimEndingDirectorySeparator(Path.GetTempPath()),
            "rehost-webforms-tempfiles"));
        Path.IsPathFullyQualified(root).ShouldBeTrue();
    }

    [Fact]
    public void Relative_configured_temp_directory_fails_naming_its_configuration_source()
    {
        var exception = Should.Throw<ConfigurationErrorsException>(() => CodegenDirectory.SelectTempRoot(
            hostSupplied: null,
            configured: "codegen",
            attributeName: "tempDirectory",
            fileName: "/app/web.config",
            lineNumber: 11));

        exception.Filename.ShouldBe("/app/web.config");
        exception.Line.ShouldBe(11);
        exception.Message.ShouldContain("tempDirectory");
    }

    [Fact]
    public void Generation_segment_is_eight_stable_hexadecimal_characters()
    {
        CodegenDirectory.GenerationSegment("/var/app").ShouldBe("c9d9badb");
        CodegenDirectory.GenerationSegment("/var/app").ShouldBe(
            CodegenDirectory.GenerationSegment("/var/app"));
    }

    [Fact]
    public void Generation_segment_separates_distinct_application_directories()
    {
        CodegenDirectory.GenerationSegment("/var/app2").ShouldBe("b64d8089");
        CodegenDirectory.GenerationSegment("/var/app2")
            .ShouldNotBe(CodegenDirectory.GenerationSegment("/var/app"));
    }

    [Fact]
    public void Generation_segment_ignores_a_trailing_separator()
    {
        CodegenDirectory.GenerationSegment("/var/app" + Path.DirectorySeparatorChar)
            .ShouldBe("c9d9badb");
    }

    [Fact]
    public void Generation_segment_folds_case_only_where_the_filesystem_does()
    {
        var upper = CodegenDirectory.GenerationSegment("/var/APP");
        var lower = CodegenDirectory.GenerationSegment("/var/app");

        if (OperatingSystem.IsWindows())
        {
            upper.ShouldBe(lower);
        }
        else
        {
            upper.ShouldNotBe(lower);
        }
    }
}
