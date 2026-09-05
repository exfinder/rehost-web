using System.Configuration;
using System.Web;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The handler walk against a transcribed-shaped baseline: first match in document order, verb
// tokens, resourceType, and the transparent extensionless row.
public sealed class IntegratedHandlersTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-iiswalk-");

    public void Dispose() => _root.Delete(recursive: true);

    private string WriteConfig(string name, string handlers)
    {
        var path = Path.Combine(_root.FullName, name);
        File.WriteAllText(
            path,
            """<?xml version="1.0"?><configuration><system.webServer><handlers>"""
            + handlers
            + "</handlers></system.webServer></configuration>");
        return path;
    }

    private string Baseline() => WriteConfig(
        "baseline.config",
        """
        <add name="PageHandlerFactory-Integrated-4.0" path="*.aspx" verb="GET,HEAD,POST,DEBUG"
             type="Base.PageHandlerFactory" preCondition="integratedMode,runtimeVersionv4.0" />
        <add name="OPTIONSVerbHandler" path="*" verb="OPTIONS" modules="ProtocolSupportModule"
             requireAccess="None" />
        <add name="ExtensionlessUrlHandler-Integrated-4.0" path="*." verb="GET,HEAD,POST,DEBUG"
             type="System.Web.Handlers.TransferRequestHandler"
             preCondition="integratedMode,runtimeVersionv4.0" />
        <add name="StaticFile" path="*" verb="*"
             modules="StaticFileModule,DefaultDocumentModule,DirectoryListingModule"
             resourceType="Either" requireAccess="Read" />
        """);

    private IReadOnlyList<IisHandlerRoute> Routes(string? applicationHandlers = null) =>
        IisServerConfiguration.Load(
                Baseline(),
                applicationHandlers == null
                    ? Path.Combine(_root.FullName, "missing", "web.config")
                    : WriteConfig("web.config", applicationHandlers))
            .HandlerRoutes;

    private static VirtualPath Path_(string virtualPath) => VirtualPath.Create(virtualPath);

    private string? Selected(IReadOnlyList<IisHandlerRoute> routes, string verb, string path) =>
        IntegratedHandlers.Selected(routes, verb, Path_(path))?.Registration.Name;

    private string Resolved(
        IReadOnlyList<IisHandlerRoute> routes, string verb, string path, string? translated = null) =>
        IntegratedHandlers.Resolve(routes, verb, Path_(path), translated).Registration.Name;

    // MH8: among the application's own adds, the first in document order wins even when a later
    // one is more specific, and both beat the inherited page factory for a real .aspx.
    [Fact]
    public void First_Match_In_Document_Order_Wins_Over_A_Later_Exact_Path()
    {
        var routes = Routes(
            """
            <add name="HB" path="*.aspx" verb="*" type="Probe.HandlerB" />
            <add name="HA" path="probe2.aspx" verb="*" type="Probe.HandlerA" />
            """);

        Selected(routes, "GET", "/app/probe2.aspx").ShouldBe("HB");
        Selected(routes, "GET", "/app/probe.aspx").ShouldBe("HB");
    }

    [Fact]
    public void An_Application_Add_Beats_The_Inherited_Page_Factory()
    {
        var routes = Routes(
            """<add name="HA" path="probe2.aspx" verb="*" type="Probe.HandlerA" />""");

        Selected(routes, "GET", "/app/probe2.aspx").ShouldBe("HA");
        Selected(routes, "GET", "/app/probe.aspx").ShouldBe("PageHandlerFactory-Integrated-4.0");
    }

    // MH9v: the re-added inherited name is active but matches at its inherited slot, after the
    // application's own fresh adds, even though it is written first.
    [Fact]
    public void A_Re_Added_Inherited_Name_Matches_After_The_Fresh_Adds()
    {
        var routes = Routes(
            """
            <remove name="ExtensionlessUrlHandler-Integrated-4.0" />
            <add name="ExtensionlessUrlHandler-Integrated-4.0" path="*." verb="*"
                 type="Probe.HandlerB" />
            <add name="ApiHandler" path="api" verb="*" type="Probe.HandlerA" />
            """);

        Selected(routes, "GET", "/app/api").ShouldBe("ApiHandler");
        Selected(routes, "GET", "/app/nosuchthing")
            .ShouldBe("ExtensionlessUrlHandler-Integrated-4.0");
    }

    // MH21: the path-matched, verb-mismatched row is skipped and the walk continues; nothing in
    // the mapping layer answers 405.
    [Fact]
    public void A_Verb_Mismatched_Row_Falls_Through_To_The_Next_Candidate()
    {
        var routes = Routes(
            """
            <add name="P" path="probe2.aspx" verb="POST" type="Probe.HandlerA" />
            <add name="W" path="*.aspx" verb="*" type="Probe.HandlerB" />
            """);

        Selected(routes, "GET", "/app/probe2.aspx").ShouldBe("W");
        Selected(routes, "POST", "/app/probe2.aspx").ShouldBe("P");
    }

    // MH28: the path pattern is case-insensitive on both sides.
    [Fact]
    public void A_Path_Pattern_Matches_Regardless_Of_Casing()
    {
        var routes = Routes(
            """
            <add name="UpperPat" path="*.ASPX" verb="*" type="Probe.HandlerA" />
            <add name="Exact" path="Marker.axd" verb="*" type="Probe.HandlerB" />
            """);

        Selected(routes, "GET", "/app/probe.aspx").ShouldBe("UpperPat");
        Selected(routes, "GET", "/app/MARKER.axd").ShouldBe("Exact");
    }

    // MH28: the verb list is compared case-sensitively, so a lowercase token refuses the
    // uppercase method and the walk moves on.
    [Fact]
    public void A_Verb_Token_Is_Compared_Case_Sensitively()
    {
        var routes = Routes(
            """<add name="LowerVerb" path="verbcase.axd" verb="get" type="Probe.HandlerA" />""");

        Selected(routes, "get", "/app/verbcase.axd").ShouldBe("LowerVerb");
        Selected(routes, "GET", "/app/verbcase.axd").ShouldBe("StaticFile");
    }

    // MH28: a pattern carrying directory segments matches on segment boundaries anywhere in the
    // path, and never against a shorter tail of it.
    [Fact]
    public void A_Multi_Segment_Pattern_Matches_On_Segment_Boundaries()
    {
        var routes = Routes(
            """<add name="SubExact" path="sub/marker.axd" verb="*" type="Probe.HandlerA" />""");

        Selected(routes, "GET", "/app/sub/marker.axd").ShouldBe("SubExact");
        Selected(routes, "GET", "/app/deeper/sub/marker.axd").ShouldBe("SubExact");
        Selected(routes, "GET", "/app/marker.axd").ShouldBe("StaticFile");
    }

    // MH9's follow-up: the child request TransferRequestHandler spawns walks the list again
    // without the spawning row, so an unrouted extensionless URL lands on StaticFile. The row is
    // still the selection preConditions see, because its type is managed.
    [Fact]
    public void The_Extensionless_Row_Is_Selected_But_Transparent_To_Dispatch()
    {
        var routes = Routes();
        var file = System.IO.Path.Combine(_root.FullName, "present");
        File.WriteAllText(file, "extensionless");

        var selected = IntegratedHandlers.Selected(routes, "GET", Path_("/app/present"));
        selected!.Registration.Name.ShouldBe("ExtensionlessUrlHandler-Integrated-4.0");
        selected.IsManaged.ShouldBeTrue();

        Resolved(routes, "GET", "/app/present", file).ShouldBe("StaticFile");
    }

    // MH18: with the catch-all removed nothing matches, and the walk answers 404 rather than
    // falling back on anything.
    [Fact]
    public void An_Unmatched_Url_Answers_404()
    {
        var routes = Routes("""<remove name="StaticFile" />""");

        var failure = Should.Throw<HttpException>(
            () => IntegratedHandlers.Resolve(routes, "GET", Path_("/app/x.txt"), null));

        failure.GetHttpCode().ShouldBe(404);
    }

    // MH26: default resourceType never asks the disk; File requires the mapped file and names
    // the entry in its 404 instead of falling through to the catch-all.
    [Fact]
    public void ResourceType_File_Answers_404_Naming_The_Entry()
    {
        var routes = Routes(
            """
            <add name="Ghost" path="ghost.aspx" verb="*" type="Probe.HandlerA" />
            <add name="Phantom" path="phantom.aspx" verb="*" type="Probe.HandlerA"
                 resourceType="File" />
            """);

        var absent = System.IO.Path.Combine(_root.FullName, "no-such-file");

        Resolved(routes, "GET", "/app/ghost.aspx", absent).ShouldBe("Ghost");

        var failure = Should.Throw<HttpException>(
            () => IntegratedHandlers.Resolve(routes, "GET", Path_("/app/phantom.aspx"), absent));

        failure.GetHttpCode().ShouldBe(404);
        failure.Message.ShouldContain("Phantom", Case.Sensitive);
    }

    // No reading covers Either and Directory; they carry the meaning IIS documents, and the
    // baseline StaticFile row's Either is what makes a missing file 404 before the static handler
    // ever runs.
    [Fact]
    public void ResourceType_Either_Accepts_A_Directory_And_Refuses_Nothing_On_Disk()
    {
        var routes = Routes();

        Resolved(routes, "GET", "/app/", _root.FullName).ShouldBe("StaticFile");

        Should.Throw<HttpException>(
                () => IntegratedHandlers.Resolve(
                    routes,
                    "GET",
                    Path_("/app/gone.txt"),
                    System.IO.Path.Combine(_root.FullName, "gone.txt")))
            .GetHttpCode()
            .ShouldBe(404);
    }

    [Fact]
    public void A_Directory_Row_Refuses_A_File()
    {
        var routes = Routes(
            """<add name="OnlyDir" path="*.aspx" verb="*" type="Probe.HandlerA" resourceType="Directory" />""");

        Resolved(routes, "GET", "/app/x.aspx", _root.FullName).ShouldBe("OnlyDir");

        var file = System.IO.Path.Combine(_root.FullName, "x.aspx");
        File.WriteAllText(file, "page");

        Should.Throw<HttpException>(
            () => IntegratedHandlers.Resolve(routes, "GET", Path_("/app/x.aspx"), file));
    }

    // A native row the port has no reimplementation for is an activation refusal naming the
    // module, not a request that silently serves nothing.
    [Fact]
    public void An_Unbridged_Native_Module_Refuses_Activation()
    {
        var failure = Should.Throw<ConfigurationErrorsException>(
            () => Routes(
                """
                <add name="Isapi" path="*.dll" verb="*" modules="IsapiModule"
                     scriptProcessor="C:\legacy.dll" />
                """));

        failure.Message.ShouldContain("IsapiModule", Case.Sensitive);
        failure.Message.ShouldContain("Isapi", Case.Sensitive);
    }

    // The bridge stays a DefaultHttpHandler because ImplicitAsyncPreloadModule and
    // HttpServerUtility.Execute type-test for one (ledger P93).
    [Fact]
    public void The_StaticFile_Bridge_Serves_Through_The_Port_Owned_Static_Handler()
    {
        var routes = Routes();
        var file = System.IO.Path.Combine(_root.FullName, "asset.txt");
        File.WriteAllText(file, "asset");

        var route = IntegratedHandlers.Resolve(routes, "GET", Path_("/app/asset.txt"), file);
        route.Registration.Name.ShouldBe("StaticFile");

        var handler = IntegratedHandlers.NativeHandler(route, "GET");

        handler.ShouldBeOfType<StaticFileBridgeHandler>();
        handler.ShouldBeAssignableTo<DefaultHttpHandler>();
    }

    [Fact]
    public void An_Unknown_ResourceType_Refuses_Activation()
    {
        Should.Throw<ConfigurationErrorsException>(
                () => Routes(
                    """<add name="Odd" path="*.aspx" verb="*" type="Probe.HandlerA" resourceType="Wherever" />"""))
            .Message.ShouldContain("Wherever", Case.Sensitive);
    }
}
