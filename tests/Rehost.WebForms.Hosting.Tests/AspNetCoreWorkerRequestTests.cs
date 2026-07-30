using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Rehost.WebForms.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

public sealed class AspNetCoreWorkerRequestTests
{
    private static readonly string PhysicalRoot =
        Path.Combine(Path.GetTempPath(), "rehost-hosting-fixture");

    [Fact]
    public void Uri_Path_Includes_The_Path_Base()
    {
        var request = Create(path: "/oracle", pathBase: "/app");

        request.GetUriPath().ShouldBe("/app/oracle");
    }

    [Fact]
    public void Uri_Path_Falls_Back_To_Root_When_The_Request_Carries_None()
    {
        var request = Create(path: "");

        request.GetUriPath().ShouldBe("/");
    }

    [Fact]
    public void Query_String_Drops_The_Leading_Question_Mark()
    {
        var request = Create(query: "?a=1&b=2");

        request.GetQueryString().ShouldBe("a=1&b=2");
    }

    [Fact]
    public void Raw_Url_Preserves_The_Encoded_Request_Target()
    {
        var context = Context(path: "/a b/c");
        context.Features.Get<IHttpRequestFeature>()!.RawTarget = "/a%20b/c?x=%2F";

        Create(context).GetRawUrl().ShouldBe("/a%20b/c?x=%2F");
    }

    [Fact]
    public void Raw_Url_Is_Reconstructed_When_The_Feature_Reports_No_Target()
    {
        var context = Context(path: "/oracle", query: "?a=1");
        context.Features.Get<IHttpRequestFeature>()!.RawTarget = null!;

        Create(context).GetRawUrl().ShouldBe("/oracle?a=1");
    }

    [Fact]
    public void Repeated_Request_Headers_Are_Joined()
    {
        var context = Context();
        context.Request.Headers["X-Repeated"] = new[] { "one", "two" };

        Create(context).GetUnknownRequestHeader("X-Repeated").ShouldBe("one, two");
    }

    [Fact]
    public void Known_Headers_Are_Excluded_From_The_Unknown_Collection()
    {
        var context = Context();
        context.Request.Headers.Host = "example.invalid";
        context.Request.Headers["X-Parity-Request"] = "cold-sync";

        var request = Create(context);

        request.GetKnownRequestHeader(System.Web.HttpWorkerRequest.HeaderHost)
            .ShouldBe("example.invalid");
        request.GetUnknownRequestHeaders()
            .ShouldHaveSingleItem()
            .ShouldBe(new[] { "X-Parity-Request", "cold-sync" });
    }

    [Fact]
    public void Absent_Headers_Report_Null()
    {
        Create().GetUnknownRequestHeader("X-Missing").ShouldBeNull();
    }

    [Fact]
    public void Missing_Connection_Data_Reports_Empty_Rather_Than_A_Placeholder()
    {
        var request = Create();

        request.GetRemoteAddress().ShouldBe("");
        request.GetLocalAddress().ShouldBe("");
    }

    [Fact]
    public void Server_Variables_Outside_The_Supported_Set_Report_Null()
    {
        Create().GetServerVariable("APPL_MD_PATH").ShouldBeNull();
    }

    [Fact]
    public void Http_Prefixed_Server_Variables_Read_Request_Headers()
    {
        var context = Context();
        context.Request.Headers["X-Parity-Request"] = "cold-sync";

        Create(context).GetServerVariable("HTTP_X_PARITY_REQUEST").ShouldBe("cold-sync");
    }

    [Fact]
    public void Supported_Server_Variables_Describe_The_Request()
    {
        var context = Context(path: "/oracle", query: "?a=1");
        context.Request.Host = new HostString("example.invalid", 8080);

        var request = Create(context);

        request.GetServerVariable("SERVER_NAME").ShouldBe("example.invalid");
        request.GetServerVariable("SERVER_PORT").ShouldBe("8080");
        request.GetServerVariable("QUERY_STRING").ShouldBe("a=1");
        request.GetServerVariable("SCRIPT_NAME").ShouldBe("/oracle");
        request.GetServerVariable("HTTPS").ShouldBe("off");
    }

    [Fact]
    public void Map_Path_Resolves_Beneath_The_Application_Root()
    {
        Create().MapPath("/bin/CoreParity.Probes.dll")
            .ShouldBe(Path.Combine(PhysicalRoot, "bin", "CoreParity.Probes.dll"));
    }

    [Fact]
    public void Map_Path_Returns_The_Application_Root_For_The_Virtual_Root()
    {
        Create().MapPath("/").ShouldBe(PhysicalRoot);
    }

    [Fact]
    public void Map_Path_Rejects_Traversal_Outside_The_Application()
    {
        Create().MapPath("/../outside/secret.config").ShouldBeNull();
    }

    [Fact]
    public void Map_Path_Rejects_Backslashes()
    {
        Create().MapPath("/bin\\probes.dll").ShouldBeNull();
    }

    [Fact]
    public void Map_Path_Rejects_Relative_Virtual_Paths()
    {
        Create().MapPath("bin/probes.dll").ShouldBeNull();
    }

    [Fact]
    public void Map_Path_Honours_A_Nested_Virtual_Root()
    {
        var request = Create(virtualRoot: "/app");

        request.MapPath("/app/bin/probes.dll")
            .ShouldBe(Path.Combine(PhysicalRoot, "bin", "probes.dll"));
        request.MapPath("/app").ShouldBe(PhysicalRoot);
    }

    [Fact]
    public void Map_Path_Rejects_Paths_Outside_A_Nested_Virtual_Root()
    {
        var request = Create(virtualRoot: "/app");

        request.MapPath("/other/probes.dll").ShouldBeNull();
        request.MapPath("/application/probes.dll").ShouldBeNull();
    }

    [Fact]
    public void Requests_Carrying_An_Entity_Body_Are_Rejected()
    {
        var context = Context();
        context.Request.ContentLength = 5;

        Should.Throw<NotSupportedException>(() => Create(context))
            .Message.ShouldContain("Request bodies");
    }

    [Fact]
    public void Chunked_Requests_Are_Rejected()
    {
        var context = Context();
        context.Request.Headers.TransferEncoding = "chunked";

        Should.Throw<NotSupportedException>(() => Create(context));
    }

    [Fact]
    public void Sending_A_Response_From_A_File_Is_Rejected()
    {
        Should.Throw<NotSupportedException>(
            () => Create().SendResponseFromFile("payload.bin", 0, 1));
    }

    [Fact]
    public void End_Of_Request_Completes_The_Request_And_Seals_The_Response()
    {
        var request = Create();

        request.Completion.IsCompleted.ShouldBeFalse();
        request.EndOfRequest();

        request.Completion.IsCompletedSuccessfully.ShouldBeTrue();
        request.Response.IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void A_Second_End_Of_Request_Throws()
    {
        var request = Create();
        request.EndOfRequest();

        Should.Throw<InvalidOperationException>(request.EndOfRequest)
            .Message.ShouldContain("more than once");
    }

    [Fact]
    public void A_Sealed_Response_Rejects_Further_Output()
    {
        var request = Create();
        request.EndOfRequest();

        Should.Throw<InvalidOperationException>(() => request.SendStatus(500, "Too Late"));
        Should.Throw<InvalidOperationException>(
            () => request.SendResponseFromMemory(new byte[] { 1 }, 1));
    }

    [Fact]
    public void Calculated_Content_Length_Is_Held_Apart_From_The_Header_List()
    {
        var request = Create();

        request.SendStatus(201, "Oracle Created");
        request.SendUnknownResponseHeader("X-Framework-Oracle", "cold-sync");
        request.SendCalculatedContentLength(9L);

        request.Response.StatusCode.ShouldBe(201);
        request.Response.ReasonPhrase.ShouldBe("Oracle Created");
        request.Response.ContentLength.ShouldBe(9);
        request.Response.Headers.ShouldHaveSingleItem().Name.ShouldBe("X-Framework-Oracle");
    }

    [Fact]
    public void Client_Disconnect_Is_Observable()
    {
        var context = Context();
        var request = Create(context);

        request.IsClientConnected().ShouldBeTrue();

        context.RequestAborted = new CancellationToken(canceled: true);

        request.IsClientConnected().ShouldBeFalse();
    }

    [Fact]
    public void A_Response_Cannot_Be_Committed_Before_It_Is_Sealed()
    {
        var request = Create();

        Should.Throw<InvalidOperationException>(
            () => request.Response.DrainAsync(Stream.Null, CancellationToken.None));
    }

    private static DefaultHttpContext Context(
        string path = "/oracle",
        string pathBase = "",
        string query = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Protocol = "HTTP/1.1";
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.PathBase = new PathString(pathBase);
        context.Request.Path = new PathString(path);
        context.Request.QueryString = new QueryString(query);
        return context;
    }

    private static AspNetCoreWorkerRequest Create(
        string path = "/oracle",
        string pathBase = "",
        string query = "",
        string virtualRoot = "/")
    {
        return Create(Context(path, pathBase, query), virtualRoot);
    }

    private static AspNetCoreWorkerRequest Create(
        HttpContext context,
        string virtualRoot = "/")
    {
        return new AspNetCoreWorkerRequest(
            context,
            virtualRoot,
            PhysicalRoot,
            Path.GetTempPath);
    }
}
