using System.Configuration;
using System.Web;
using System.Web.IisConfig;
using Rehost.WebForms.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// Per-folder <handlers> resolution: the same merge algorithm applied one level per folder
// web.config down the request's directory, queried by path. Compositions MH27 does not pin are
// named "Unmeasured" and assert what applying the per-level algorithm level by level produces.
public sealed class IisFolderHandlersTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("rehost-iisfolder-");

    public void Dispose() => _temp.Delete(recursive: true);

    private string AppRoot => Path.Combine(_temp.FullName, "app");

    private const string AppVirtualPath = "/app";

    private const string RootWildcard =
        """<add name="RootW" path="*.aspx" verb="*" type="Probe.HandlerB" />""";

    private string WriteRaw(string relativeDirectory, string body)
    {
        var directory = Path.Combine(AppRoot, relativeDirectory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "web.config");
        File.WriteAllText(path, """<?xml version="1.0"?><configuration>""" + body + "</configuration>");
        return path;
    }

    private string Write(string relativeDirectory, string handlers) => WriteRaw(
        relativeDirectory,
        "<system.webServer><handlers>" + handlers + "</handlers></system.webServer>");

    private string Baseline()
    {
        var path = Path.Combine(_temp.FullName, "baseline.config");
        File.WriteAllText(
            path,
            """
            <?xml version="1.0"?>
            <configuration>
              <system.webServer>
                <security>
                  <requestFiltering>
                    <hiddenSegments><add segment="App_Data" /></hiddenSegments>
                  </requestFiltering>
                </security>
                <handlers>
                  <add name="PageHandlerFactory-Integrated-4.0" path="*.aspx"
                       verb="GET,HEAD,POST,DEBUG" type="Base.PageHandlerFactory"
                       preCondition="integratedMode,runtimeVersionv4.0" />
                  <add name="StaticFile" path="*" verb="*"
                       modules="StaticFileModule,DefaultDocumentModule,DirectoryListingModule"
                       resourceType="Either" requireAccess="Read" />
                </handlers>
              </system.webServer>
            </configuration>
            """);
        return path;
    }

    private IisServerConfiguration Load() => IisServerConfiguration.Load(
        Baseline(), Path.Combine(AppRoot, "web.config"), AppVirtualPath);

    private static string? Selected(
        IisServerConfiguration configuration, string path, string verb = "GET")
    {
        var virtualPath = VirtualPath.Create(path);
        return IntegratedHandlers
            .Selected(configuration.HandlerRoutesFor(virtualPath), verb, virtualPath)
            ?.Registration.Name;
    }

    // Activation reads every folder file, so the message has to name the one that is broken;
    // XmlException carries only a line and a position.
    [Fact]
    public void A_Malformed_Folder_Config_Fails_Naming_The_File()
    {
        Write(string.Empty, RootWildcard);
        var broken = Path.Combine(AppRoot, "sub", "web.config");
        Directory.CreateDirectory(Path.GetDirectoryName(broken)!);
        File.WriteAllText(broken, """<?xml version="1.0"?><configuration><system.webServer>""");

        var exception = Should.Throw<ConfigurationErrorsException>(Load);

        exception.Message.ShouldContain(broken);
        exception.Message.ShouldContain("not well-formed XML", Case.Sensitive);
    }

    // A hidden segment refuses every request that touches it, so a folder file underneath one can
    // never be selected. The malformed file is the detector: reaching it would fail activation.
    [Fact]
    public void A_Folder_Below_A_Hidden_Segment_Is_Never_Read()
    {
        Write(string.Empty, RootWildcard);
        var unreachable = Path.Combine(AppRoot, "App_Data", "deep", "web.config");
        Directory.CreateDirectory(Path.GetDirectoryName(unreachable)!);
        File.WriteAllText(unreachable, "not xml at all");

        var configuration = Load();

        Selected(configuration, "/app/probe.aspx").ShouldBe("RootW");
    }

    // A directory the process cannot list would otherwise be skipped in silence, answering its
    // requests from the nearest readable ancestor - a looser policy than the folder declared.
    [Fact]
    public void An_Unreadable_Directory_Fails_Activation()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Read permission is removed with chmod, which NTFS does not model.");
            return;
        }

        Assert.SkipWhen(
            Environment.IsPrivilegedProcess, "A privileged process ignores the mode bits.");

        Write(string.Empty, RootWildcard);
        var locked = Path.Combine(AppRoot, "locked");
        Directory.CreateDirectory(locked);
        File.WriteAllText(Path.Combine(locked, "web.config"), "<configuration />");
        File.SetUnixFileMode(locked, UnixFileMode.None);

        try
        {
            Should.Throw<ConfigurationErrorsException>(Load)
                .Message.ShouldContain("cannot be read");
        }
        finally
        {
            File.SetUnixFileMode(
                locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    // MH27a: both levels claim *.aspx and the child's add wins inside the folder, while the
    // parent and the root keep the parent's.
    [Fact]
    public void The_Deepest_Add_Wins_For_The_Same_Pattern()
    {
        Write(string.Empty, RootWildcard);
        Write("sub", """<add name="SubW" path="*.aspx" verb="*" type="Probe.HandlerA" />""");

        var configuration = Load();

        Selected(configuration, "/app/sub/probe.aspx").ShouldBe("SubW");
        Selected(configuration, "/app/probe.aspx").ShouldBe("RootW");
        Selected(configuration, "/app/other/probe.aspx").ShouldBe("RootW");
    }

    // MH27b: the folder removes the application-level add and matching falls back to the
    // inherited page factory there, with the root still on its own add.
    [Fact]
    public void A_Folder_Remove_Of_A_Parent_Add_Falls_Back_To_The_Inherited_Row()
    {
        Write(string.Empty, RootWildcard);
        Write("sub", """<remove name="RootW" />""");

        var configuration = Load();

        Selected(configuration, "/app/sub/probe.aspx")
            .ShouldBe("PageHandlerFactory-Integrated-4.0");
        Selected(configuration, "/app/probe.aspx").ShouldBe("RootW");
    }

    // A directory below a configured folder carries that folder's list: resolution walks up to
    // the deepest configured ancestor.
    [Fact]
    public void A_Directory_Below_A_Configured_Folder_Inherits_Its_List()
    {
        Write(string.Empty, RootWildcard);
        Write("sub", """<add name="SubW" path="*.aspx" verb="*" type="Probe.HandlerA" />""");

        var configuration = Load();

        Selected(configuration, "/app/sub/deeper/probe.aspx").ShouldBe("SubW");
    }

    // Unmeasured composition: MH27a measured two levels. Applying the same rule per level makes
    // the deepest of three win, with each intermediate folder keeping its own.
    [Fact]
    public void Unmeasured_Three_Levels_Are_Consulted_Deepest_First()
    {
        Write(string.Empty, RootWildcard);
        Write("a", """<add name="AW" path="*.aspx" verb="*" type="Probe.HandlerA" />""");
        Write("a/b", """<add name="BW" path="*.aspx" verb="*" type="Probe.HandlerC" />""");

        var configuration = Load();

        Selected(configuration, "/app/a/b/probe.aspx").ShouldBe("BW");
        Selected(configuration, "/app/a/probe.aspx").ShouldBe("AW");
        Selected(configuration, "/app/probe.aspx").ShouldBe("RootW");
    }

    // Unmeasured composition: a folder removing the parent's add and re-adding the same name.
    // The re-add is a fresh add of the folder's own level, because the removed name was the
    // parent's fresh add and not an inherited slot, so it is consulted ahead of everything
    // shallower.
    [Fact]
    public void Unmeasured_A_Folder_Remove_And_Re_Add_Of_A_Parent_Name_Is_A_Fresh_Folder_Add()
    {
        Write(
            string.Empty,
            """
            <add name="Wild" path="*.aspx" verb="*" type="Probe.HandlerB" />
            <add name="Exact" path="probe.aspx" verb="*" type="Probe.HandlerC" />
            """);
        Write(
            "sub",
            """<remove name="Exact" /><add name="Exact" path="probe.aspx" verb="*" type="Probe.HandlerA" />""");

        var configuration = Load();

        Selected(configuration, "/app/sub/probe.aspx").ShouldBe("Exact");
        Selected(configuration, "/app/probe.aspx").ShouldBe("Wild");
    }

    // MH9v applied per folder, unmeasured there: an inherited name the folder re-adds keeps its
    // inherited slot, so the folder's other fresh add is still consulted first.
    [Fact]
    public void Unmeasured_A_Folder_Re_Adding_An_Inherited_Name_Keeps_Its_Inherited_Slot()
    {
        Write(string.Empty, string.Empty);
        Write(
            "sub",
            """
            <remove name="PageHandlerFactory-Integrated-4.0" />
            <add name="PageHandlerFactory-Integrated-4.0" path="*.aspx" verb="*"
                 type="Probe.HandlerB" />
            <add name="SubExact" path="probe.aspx" verb="*" type="Probe.HandlerA" />
            """);

        var configuration = Load();

        Selected(configuration, "/app/sub/probe.aspx").ShouldBe("SubExact");
        Selected(configuration, "/app/sub/other.aspx").ShouldBe("PageHandlerFactory-Integrated-4.0");
    }

    // Unmeasured composition: <clear/> is honored for handlers (MH18) and a folder's clear empties
    // that folder's list only, so the folder answers 404 where the root still serves.
    [Fact]
    public void Unmeasured_A_Folder_Clear_Empties_That_Folder_Only()
    {
        Write(string.Empty, RootWildcard);
        Write("sub", "<clear />");

        var configuration = Load();

        Selected(configuration, "/app/sub/probe.aspx").ShouldBeNull();
        Selected(configuration, "/app/probe.aspx").ShouldBe("RootW");
    }

    // Unmeasured composition: adding a name that is live above without removing it first is the
    // duplicate IIS answers with 500.19, as it is within one level (MH13).
    [Fact]
    public void Unmeasured_A_Folder_Add_Over_A_Live_Parent_Name_Refuses_Naming_The_Folder_File()
    {
        Write(string.Empty, """<add name="Dup" path="a.axd" verb="*" type="Probe.HandlerB" />""");
        var folder = Write(
            "sub", """<add name="Dup" path="b.axd" verb="*" type="Probe.HandlerA" />""");

        var failure = Should.Throw<ConfigurationErrorsException>(Load);

        failure.Message.ShouldContain(folder);
        failure.Message.ShouldContain("Dup", Case.Sensitive);
    }

    // A folder's rows are validated at activation, not when a request first reaches the folder.
    [Fact]
    public void A_Folder_Row_With_An_Unknown_PreCondition_Refuses_Activation()
    {
        Write(string.Empty, RootWildcard);
        var folder = Write(
            "sub",
            """<add name="Bogus" path="*.axd" verb="*" type="Probe.HandlerA" preCondition="nonsense" />""");

        var failure = Should.Throw<ConfigurationErrorsException>(Load);

        failure.Message.ShouldContain(folder);
        failure.Message.ShouldContain("nonsense");
    }

    [Fact]
    public void A_Folder_Row_Naming_An_Unbridged_Native_Module_Refuses_Activation()
    {
        var folder = Write(
            "sub",
            """<add name="Native" path="*" verb="*" modules="CustomNativeModule" />""");

        var failure = Should.Throw<ConfigurationErrorsException>(Load);

        failure.Message.ShouldContain(folder);
        failure.Message.ShouldContain("CustomNativeModule", Case.Sensitive);
    }

    // MH24: IIS ignores a folder <modules> section outright. The handlers walk reads the same
    // file and must not start honoring it.
    [Fact]
    public void A_Folder_Modules_Section_Is_Ignored()
    {
        Write(string.Empty, RootWildcard);
        WriteRaw(
            "sub",
            """
            <system.webServer>
              <modules>
                <add name="FolderModule" type="Probe.ModuleA" />
              </modules>
              <handlers>
                <add name="SubW" path="*.aspx" verb="*" type="Probe.HandlerA" />
              </handlers>
            </system.webServer>
            """);

        var configuration = Load();

        configuration.Modules.ShouldNotContain(module => module.Name == "FolderModule");
        Selected(configuration, "/app/sub/probe.aspx").ShouldBe("SubW");
    }

    // The folder file carries no <handlers>, so the folder keeps the application root's list.
    [Fact]
    public void A_Folder_File_Without_Handlers_Leaves_The_Root_List_In_Place()
    {
        Write(string.Empty, RootWildcard);
        WriteRaw("sub", """<system.web><pages enableViewState="false" /></system.web>""");

        var configuration = Load();

        Selected(configuration, "/app/sub/probe.aspx").ShouldBe("RootW");
    }

    // The classic-section rule (MH5) reaches folder files too, with the waiver inherited from
    // above. Unmeasured: MH5 measured the application root.
    [Fact]
    public void Unmeasured_Folder_Classic_Content_Refuses_When_The_Waiver_Is_Absent()
    {
        Write(string.Empty, RootWildcard);
        var folder = WriteRaw(
            "sub",
            """
            <system.web>
              <httpHandlers>
                <add path="legacy.axd" verb="*" type="Probe.HandlerA" />
              </httpHandlers>
            </system.web>
            """);

        var failure = Should.Throw<ConfigurationErrorsException>(Load);

        failure.Message.ShouldContain(folder);
        failure.Message.ShouldContain("legacy.axd");
    }

    [Fact]
    public void Unmeasured_Folder_Classic_Content_Is_Dead_Text_Under_The_Inherited_Waiver()
    {
        WriteRaw(
            string.Empty,
            """<system.webServer><validation validateIntegratedModeConfiguration="false" /><handlers>""" + RootWildcard + "</handlers></system.webServer>");
        WriteRaw(
            "sub",
            """
            <system.web>
              <httpHandlers>
                <add path="legacy.axd" verb="*" type="Probe.HandlerA" />
              </httpHandlers>
            </system.web>
            """);

        var configuration = Load();

        Selected(configuration, "/app/sub/legacy.axd").ShouldBe("StaticFile");
    }

    [Fact]
    public void Unmeasured_A_Folder_Can_Restate_The_Waiver_As_True_And_Refuse()
    {
        WriteRaw(
            string.Empty,
            """<system.webServer><validation validateIntegratedModeConfiguration="false" /><handlers>""" + RootWildcard + "</handlers></system.webServer>");
        var folder = WriteRaw(
            "sub",
            """
            <system.web>
              <httpHandlers>
                <add path="legacy.axd" verb="*" type="Probe.HandlerA" />
              </httpHandlers>
            </system.web>
            <system.webServer>
              <validation validateIntegratedModeConfiguration="true" />
            </system.webServer>
            """);

        Should.Throw<ConfigurationErrorsException>(Load).Message.ShouldContain(folder);
    }

    // Visual Studio writes Web.config; the configuration system composes web.config. The folder
    // is found either way, on every filesystem (ledger P70).
    [Fact]
    public void A_Folder_Config_Is_Found_Whatever_Its_File_Name_Casing()
    {
        Write(string.Empty, RootWildcard);
        var directory = Path.Combine(AppRoot, "sub");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "Web.config"),
            """
            <?xml version="1.0"?>
            <configuration>
              <system.webServer>
                <security>
                  <requestFiltering>
                    <hiddenSegments><add segment="App_Data" /></hiddenSegments>
                  </requestFiltering>
                </security>
                <handlers>
                  <add name="SubW" path="*.aspx" verb="*" type="Probe.HandlerA" />
                </handlers>
              </system.webServer>
            </configuration>
            """);

        Selected(Load(), "/app/sub/probe.aspx").ShouldBe("SubW");
    }

    // The request's own casing does not decide which folder record answers: a configuration path
    // is a lowercased virtual path.
    [Fact]
    public void A_Folder_Record_Answers_Whatever_The_Requested_Casing()
    {
        Write(string.Empty, RootWildcard);
        Write("Sub", """<add name="SubW" path="*.aspx" verb="*" type="Probe.HandlerA" />""");

        var configuration = Load();

        Selected(configuration, "/app/sub/probe.aspx").ShouldBe("SubW");
        Selected(configuration, "/app/SUB/probe.aspx").ShouldBe("SubW");
    }
}

