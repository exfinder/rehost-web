using System.Configuration;
using System.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

public sealed class CodegenDirectoryTests
{
    [Fact]
    public void Host_Supplied_Root_Wins_Over_Configured_Temp_Directory()
    {
        var root = CodegenDirectory.SelectTempRoot(
            hostSupplied: Path.Combine(Path.GetTempPath(), "host-root"),
            configured: Path.Combine(Path.GetTempPath(), "configured-root"),
            attributeName: "tempDirectory",
            fileName: "web.config",
            lineNumber: 11,
            defaultTempRoot: Path.Combine(Path.GetTempPath(), "default-root"));

        root.ShouldBe(Path.Combine(Path.GetTempPath(), "host-root"));
    }

    [Fact]
    public void Configured_Temp_Directory_Is_Used_When_No_Host_Root_Exists()
    {
        var configured = Path.Combine(Path.GetTempPath(), "configured-root");

        var root = CodegenDirectory.SelectTempRoot(
            hostSupplied: null,
            configured: configured + Path.DirectorySeparatorChar,
            attributeName: "tempDirectory",
            fileName: "web.config",
            lineNumber: 11,
            defaultTempRoot: Path.Combine(Path.GetTempPath(), "default-root"));

        root.ShouldBe(configured);
    }

    [Fact]
    public void Absent_Root_Falls_Back_To_The_Supplied_Default()
    {
        var root = CodegenDirectory.SelectTempRoot(
            hostSupplied: null,
            configured: null,
            attributeName: null,
            fileName: null,
            lineNumber: 0,
            defaultTempRoot: Path.Combine(Path.GetTempPath(), "default-root"));

        root.ShouldBe(Path.Combine(Path.GetTempPath(), "default-root"));
    }

    [Fact]
    public void Relative_Configured_Temp_Directory_Fails_Naming_Its_Configuration_Source()
    {
        var exception = Should.Throw<ConfigurationErrorsException>(() => CodegenDirectory.SelectTempRoot(
            hostSupplied: null,
            configured: "codegen",
            attributeName: "tempDirectory",
            fileName: "/app/web.config",
            lineNumber: 11,
            defaultTempRoot: Path.Combine(Path.GetTempPath(), "default-root")));

        exception.Filename.ShouldBe("/app/web.config");
        exception.Line.ShouldBe(11);
        exception.Message.ShouldContain("tempDirectory");
    }

    [Fact]
    public void Generation_Segment_Is_Eight_Stable_Hexadecimal_Characters()
    {
        CodegenDirectory.GenerationSegment("/var/app").ShouldBe("c9d9badb");
    }

    [Fact]
    public void Generation_Segment_Separates_Distinct_Application_Directories()
    {
        CodegenDirectory.GenerationSegment("/var/app2").ShouldBe("b64d8089");
        CodegenDirectory.GenerationSegment("/var/app2")
            .ShouldNotBe(CodegenDirectory.GenerationSegment("/var/app"));
    }

    [Fact]
    public void Generation_Segment_Ignores_A_Trailing_Separator()
    {
        CodegenDirectory.GenerationSegment("/var/app" + Path.DirectorySeparatorChar)
            .ShouldBe("c9d9badb");
    }

    [Fact]
    public void Generation_Segment_Folds_Case_Only_Where_The_Filesystem_Does()
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
