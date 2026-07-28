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
    public void Uri_path_includes_the_path_base()
    {
        var request = Create(path: "/oracle", pathBase: "/app");

        request.GetUriPath().ShouldBe("/app/oracle");
    }

    [Fact]
    public void Uri_path_falls_back_to_root_when_the_request_carries_none()
    {
        var request = Create(path: "");

        request.GetUriPath().ShouldBe("/");
    }

    [Fact]
    public void Query_string_drops_the_leading_question_mark()
    {
        var request = Create(query: "?a=1&b=2");

        request.GetQueryString().ShouldBe("a=1&b=2");
    }

    [Fact]
    public void Raw_url_preserves_the_encoded_request_target()
    {
        var context = Context(path: "/a b/c");
        context.Features.Get<IHttpRequestFeature>()!.RawTarget = "/a%20b/c?x=%2F";

        Create(context).GetRawUrl().ShouldBe("/a%20b/c?x=%2F");
    }

    [Fact]
    public void Raw_url_is_reconstructed_when_the_feature_reports_no_target()
    {
        var context = Context(path: "/oracle", query: "?a=1");
        context.Features.Get<IHttpRequestFeature>()!.RawTarget = null!;

        Create(context).GetRawUrl().ShouldBe("/oracle?a=1");
    }

    [Fact]
    public void Repeated_request_headers_are_joined()
    {
        var context = Context();
        context.Request.Headers["X-Repeated"] = new[] { "one", "two" };

        Create(context).GetUnknownRequestHeader("X-Repeated").ShouldBe("one, two");
    }

    [Fact]
    public void Known_headers_are_excluded_from_the_unknown_collection()
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
    public void Absent_headers_report_null()
    {
        Create().GetUnknownRequestHeader("X-Missing").ShouldBeNull();
    }

    [Fact]
    public void Missing_connection_data_reports_empty_rather_than_a_placeholder()
    {
        var request = Create();

        request.GetRemoteAddress().ShouldBe("");
        request.GetLocalAddress().ShouldBe("");
    }

    [Fact]
    public void Server_variables_outside_the_supported_set_report_null()
    {
        Create().GetServerVariable("APPL_MD_PATH").ShouldBeNull();
    }

    [Fact]
    public void Http_prefixed_server_variables_read_request_headers()
    {
        var context = Context();
        context.Request.Headers["X-Parity-Request"] = "cold-sync";

        Create(context).GetServerVariable("HTTP_X_PARITY_REQUEST").ShouldBe("cold-sync");
    }

    [Fact]
    public void Supported_server_variables_describe_the_request()
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
    public void Map_path_resolves_beneath_the_application_root()
    {
        Create().MapPath("/bin/CoreParity.Probes.dll")
            .ShouldBe(Path.Combine(PhysicalRoot, "bin", "CoreParity.Probes.dll"));
    }

    [Fact]
    public void Map_path_returns_the_application_root_for_the_virtual_root()
    {
        Create().MapPath("/").ShouldBe(PhysicalRoot);
    }

    [Fact]
    public void Map_path_rejects_traversal_outside_the_application()
    {
        Create().MapPath("/../outside/secret.config").ShouldBeNull();
    }

    [Fact]
    public void Map_path_rejects_backslashes()
    {
        Create().MapPath("/bin\\probes.dll").ShouldBeNull();
    }

    [Fact]
    public void Map_path_rejects_relative_virtual_paths()
    {
        Create().MapPath("bin/probes.dll").ShouldBeNull();
    }

    [Fact]
    public void Map_path_honours_a_nested_virtual_root()
    {
        var request = Create(virtualRoot: "/app");

        request.MapPath("/app/bin/probes.dll")
            .ShouldBe(Path.Combine(PhysicalRoot, "bin", "probes.dll"));
        request.MapPath("/app").ShouldBe(PhysicalRoot);
    }

    [Fact]
    public void Map_path_rejects_paths_outside_a_nested_virtual_root()
    {
        var request = Create(virtualRoot: "/app");

        request.MapPath("/other/probes.dll").ShouldBeNull();
        request.MapPath("/application/probes.dll").ShouldBeNull();
    }

    [Fact]
    public void Requests_carrying_an_entity_body_are_rejected()
    {
        var context = Context();
        context.Request.ContentLength = 5;

        Should.Throw<NotSupportedException>(() => Create(context))
            .Message.ShouldContain("Request bodies");
    }

    [Fact]
    public void Chunked_requests_are_rejected()
    {
        var context = Context();
        context.Request.Headers.TransferEncoding = "chunked";

        Should.Throw<NotSupportedException>(() => Create(context));
    }

    [Fact]
    public void Sending_a_response_from_a_file_is_rejected()
    {
        Should.Throw<NotSupportedException>(
            () => Create().SendResponseFromFile("payload.bin", 0, 1));
    }

    [Fact]
    public void End_of_request_completes_the_request_and_seals_the_response()
    {
        var request = Create();

        request.Completion.IsCompleted.ShouldBeFalse();
        request.EndOfRequest();

        request.Completion.IsCompletedSuccessfully.ShouldBeTrue();
        request.Response.IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void A_second_end_of_request_throws()
    {
        var request = Create();
        request.EndOfRequest();

        Should.Throw<InvalidOperationException>(request.EndOfRequest)
            .Message.ShouldContain("more than once");
    }

    [Fact]
    public void A_sealed_response_rejects_further_output()
    {
        var request = Create();
        request.EndOfRequest();

        Should.Throw<InvalidOperationException>(() => request.SendStatus(500, "Too Late"));
        Should.Throw<InvalidOperationException>(
            () => request.SendResponseFromMemory(new byte[] { 1 }, 1));
    }

    [Fact]
    public void Calculated_content_length_is_held_apart_from_the_header_list()
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
    public void Client_disconnect_is_observable()
    {
        var context = Context();
        var request = Create(context);

        request.IsClientConnected().ShouldBeTrue();

        context.RequestAborted = new CancellationToken(canceled: true);

        request.IsClientConnected().ShouldBeFalse();
    }

    [Fact]
    public void A_response_cannot_be_committed_before_it_is_sealed()
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
