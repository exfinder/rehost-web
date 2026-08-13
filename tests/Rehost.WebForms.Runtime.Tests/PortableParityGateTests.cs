using System.Xml;
using Rehost.WebForms.Parity.Harness;
using Shouldly;
using Xunit;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Runtime.Tests;

// Running a session permanently mutates process-global state (the default AssemblyLoadContext
// resolver, the HttpRuntime singleton, the activated application), so every session runs in its
// own child process; orchestration and comparison stay in this process.
public sealed class PortableParityGateTests
{
    private static readonly ParityGateRunner Gate = new("Rehost.WebForms.Parity.PortableHost");

    public static TheoryData<string> Sessions
    {
        get
        {
            var sessions = new TheoryData<string>();
            foreach (var name in Gate.SessionNames)
            {
                sessions.Add(name);
            }

            return sessions;
        }
    }

    [Fact]
    public void Golden_Trace_Uses_The_Current_Schema()
    {
        Gate.GoldenSchemaVersion.ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(Sessions))]
    public void Session_Matches_The_Framework_Golden_Trace(string session)
    {
        Gate.VerifySession(session, TraceComparison.Strict);
    }

    // BuildManager combined the preserved hash file path with an embedded backslash, which off
    // Windows produced one file named "hash\hash.web" beside the codegen root instead of a file
    // inside it. Nothing observable in the response changes, so only the artifact detects it.
    // The generation segment is observable the same way: the two fixtures share one temp root and
    // must not share a segment.
    [Fact]
    public void Generated_Output_Nests_Under_One_Segment_Per_Application()
    {
        // An isolated fixture copy: codegen assertions must not race the verify sessions or
        // mutate the host's build output.
        var root = Directory.CreateTempSubdirectory("parity-codegen-");

        try
        {
            // The staged fixture root also holds temp/, carrying codegen output from earlier
            // sessions; copying it would pre-populate the segment root being asserted on.
            foreach (var fixture in Directory.GetDirectories(Gate.DefaultFixtureRoot))
            {
                var name = Path.GetFileName(fixture);
                if (name != "temp")
                {
                    TestFiles.CopyDirectory(fixture, Path.Combine(root.FullName, name));
                }
            }

            var temp = Path.Combine(root.FullName, "temp");
            // The fixture pins <compilation tempDirectory> to the staged location; repoint it so
            // the host option and the fixture still agree.
            RepointCompilationTempDirectory(
                Path.Combine(root.FullName, "app", "web.config"),
                temp);

            // One session per fixture is enough: the claim is about segment layout, not about
            // which sessions ran.
            foreach (var group in Gate.Sessions.GroupBy(session => session.Fixture))
            {
                Gate.RunSession(group.First().Name, root.FullName);
            }

            var codegenRoot = Path.Combine(temp, "root");
            Directory.EnumerateFiles(codegenRoot).ShouldBeEmpty();

            var segments = Directory.GetDirectories(codegenRoot);
            segments.Length.ShouldBe(2, "the app and app-errors fixtures each own a segment");

            foreach (var segment in segments)
            {
                Path.GetFileName(segment).ShouldMatch("^[0-9a-f]{8}$");
                File.Exists(Path.Combine(segment, "hash", "hash.web")).ShouldBeTrue();
            }

            Directory
                .EnumerateFileSystemEntries(codegenRoot, "*", SearchOption.AllDirectories)
                .ShouldNotContain(entry => Path.GetFileName(entry).Contains('\\'));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static void RepointCompilationTempDirectory(string webConfigPath, string temp)
    {
        var document = new XmlDocument();
        document.Load(webConfigPath);
        var compilation = document.SelectSingleNode("/configuration/system.web/compilation")!;
        compilation.Attributes!["tempDirectory"]!.Value = temp;
        document.Save(webConfigPath);
    }

}
