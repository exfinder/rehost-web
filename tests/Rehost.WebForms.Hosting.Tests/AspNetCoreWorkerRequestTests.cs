using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Connections;
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
    public void Body_Capable_Requests_Have_No_Preloaded_Prefix()
    {
        var context = Context();
        context.Request.ContentLength = 5;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(true));

        var request = Create(context);

        request.GetPreloadedEntityBody().ShouldBeNull();
        request.GetPreloadedEntityBodyLength().ShouldBe(0);
        request.IsEntireEntityBodyIsPreloaded().ShouldBeFalse();
    }

    [Fact]
    public void Definite_No_Body_Is_Wholly_Preloaded()
    {
        var context = Context();
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(false));

        Create(context).IsEntireEntityBodyIsPreloaded().ShouldBeTrue();
    }

    [Fact]
    public void Synchronous_Read_Uses_The_Body_Reader_And_Preserves_Offset()
    {
        var context = BodyContext("abcdef");
        var request = Create(context);
        var buffer = Enumerable.Repeat((byte)'_', 8).ToArray();

        request.ReadEntityBody(buffer, 2, 3).ShouldBe(3);
        request.ReadEntityBody(buffer, 5, 3).ShouldBe(3);
        request.ReadEntityBody(buffer, 0, 1).ShouldBe(0);

        Encoding.UTF8.GetString(buffer).ShouldBe("__abcdef");
    }

    [Fact]
    public void Zero_Count_Read_Does_Not_Consume_The_Body()
    {
        var request = Create(BodyContext("body"));
        var buffer = new byte[4];

        request.ReadEntityBody(buffer, 0, 0).ShouldBe(0);
        request.ReadEntityBody(buffer, 4).ShouldBe(4);

        Encoding.UTF8.GetString(buffer).ShouldBe("body");
    }

    [Fact]
    public void Synchronous_Read_Never_Uses_The_Request_Stream_Synchronously()
    {
        var context = Context();
        context.Request.Body = new AsyncOnlyBodyStream(Encoding.UTF8.GetBytes("body"));
        context.Request.ContentLength = 4;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(true));
        var buffer = new byte[4];

        Create(context).ReadEntityBody(buffer, 4).ShouldBe(4);

        Encoding.UTF8.GetString(buffer).ShouldBe("body");
    }

    [Fact]
    public void Completed_Apm_Read_Preserves_State_Callback_And_Result()
    {
        var request = Create(BodyContext("body"));
        var buffer = new byte[4];
        var state = new object();
        IAsyncResult? callbackResult = null;
        var callbacks = 0;

        var result = request.BeginRead(buffer, 0, 4, completed =>
        {
            callbacks++;
            callbackResult = completed;
        }, state);

        result.AsyncState.ShouldBeSameAs(state);
        result.CompletedSynchronously.ShouldBeTrue();
        result.IsCompleted.ShouldBeTrue();
        callbackResult.ShouldBeSameAs(result);
        callbacks.ShouldBe(1);
        request.EndRead(result).ShouldBe(4);
        Encoding.UTF8.GetString(buffer).ShouldBe("body");
    }

    [Fact]
    public async Task Pending_Apm_Read_Completes_Asynchronously_And_Allows_The_Next_Read()
    {
        var pipe = new Pipe();
        var context = BodyContext(pipe.Reader, contentLength: null);
        var request = Create(context);
        var firstBuffer = new byte[3];
        var secondBuffer = new byte[3];
        var callback = new TaskCompletionSource<IAsyncResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var first = request.BeginRead(firstBuffer, 0, 3, result => callback.SetResult(result), "first");
        first.CompletedSynchronously.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(
            () => request.BeginRead(secondBuffer, 0, 3, null!, null!));

        await pipe.Writer.WriteAsync(
            Encoding.UTF8.GetBytes("one"),
            TestContext.Current.CancellationToken);
        (await callback.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken)).ShouldBeSameAs(first);
        request.EndRead(first).ShouldBe(3);

        var second = request.BeginRead(secondBuffer, 0, 3, null!, "second");
        await pipe.Writer.WriteAsync(
            Encoding.UTF8.GetBytes("two"),
            TestContext.Current.CancellationToken);
        request.EndRead(second).ShouldBe(3);

        Encoding.UTF8.GetString(firstBuffer).ShouldBe("one");
        Encoding.UTF8.GetString(secondBuffer).ShouldBe("two");
        await pipe.Writer.CompleteAsync();
    }

    [Fact]
    public async Task Unconsumed_Buffered_Bytes_Are_Immediately_Readable_From_A_Live_Pipe()
    {
        var pipe = new Pipe();
        await pipe.Writer.WriteAsync(
            Encoding.UTF8.GetBytes("abcdef"),
            TestContext.Current.CancellationToken);
        var request = Create(BodyContext(pipe.Reader, contentLength: null));
        var first = new byte[3];
        var second = new byte[3];

        request.ReadEntityBody(first, 3).ShouldBe(3);
        var read = request.BeginRead(second, 0, 3, null!, null!);

        read.CompletedSynchronously.ShouldBeTrue();
        request.EndRead(read).ShouldBe(3);
        Encoding.UTF8.GetString(first).ShouldBe("abc");
        Encoding.UTF8.GetString(second).ShouldBe("def");
        await pipe.Writer.CompleteAsync();
    }

    [Fact]
    public void Synchronous_And_Apm_Reads_Can_Alternate()
    {
        var request = Create(BodyContext("onetwo"));
        var first = new byte[3];
        var second = new byte[3];

        request.ReadEntityBody(first, 3).ShouldBe(3);
        var pending = request.BeginRead(second, 0, 3, null!, null!);
        request.EndRead(pending).ShouldBe(3);

        Encoding.UTF8.GetString(first).ShouldBe("one");
        Encoding.UTF8.GetString(second).ShouldBe("two");
    }

    [Fact]
    public async Task Non_Disconnect_Failure_Is_Preserved_For_Sync_And_Wrapped_For_Apm()
    {
        var syncPipe = new Pipe();
        await syncPipe.Writer.CompleteAsync(new IOException("broken sync"));
        var sync = Create(BodyContext(syncPipe.Reader, contentLength: null));

        Should.Throw<IOException>(() => sync.ReadEntityBody(new byte[1], 1))
            .Message.ShouldBe("broken sync");
        Should.Throw<IOException>(() => sync.ReadEntityBody(new byte[1], 1))
            .Message.ShouldBe("broken sync");
        Should.Throw<System.Web.HttpException>(
                () => sync.BeginRead(new byte[1], 0, 1, null!, null!))
            .InnerException.ShouldBeOfType<IOException>()
            .Message.ShouldBe("broken sync");

        var asyncPipe = new Pipe();
        await asyncPipe.Writer.CompleteAsync(new IOException("broken async"));
        var asyncRequest = Create(BodyContext(asyncPipe.Reader, contentLength: null));

        var exception = Should.Throw<System.Web.HttpException>(
            () => asyncRequest.BeginRead(new byte[1], 0, 1, null!, null!));
        exception.InnerException.ShouldBeOfType<IOException>()
            .Message.ShouldBe("broken async");
    }

    [Fact]
    public async Task Pending_Apm_Failure_Becomes_The_Immutable_Terminal_State()
    {
        var pipe = new Pipe();
        var request = Create(BodyContext(pipe.Reader, contentLength: null));
        var completed = new TaskCompletionSource<IAsyncResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var read = request.BeginRead(
            new byte[1],
            0,
            1,
            result => completed.SetResult(result),
            null!);

        await pipe.Writer.CompleteAsync(new IOException("pending failure"));
        await completed.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Should.Throw<System.Web.HttpException>(() => request.EndRead(read))
            .InnerException.ShouldBeOfType<IOException>()
            .Message.ShouldBe("pending failure");
        Should.Throw<IOException>(() => request.ReadEntityBody(new byte[1], 1))
            .Message.ShouldBe("pending failure");
        Should.Throw<System.Web.HttpException>(
                () => request.BeginRead(new byte[1], 0, 1, null!, null!))
            .InnerException.ShouldBeOfType<IOException>()
            .Message.ShouldBe("pending failure");
    }

    [Fact]
    public void Disconnect_During_Synchronous_Read_Is_Eof()
    {
        var pipe = new Pipe();
        var context = BodyContext(pipe.Reader, contentLength: null);
        context.RequestAborted = new CancellationToken(canceled: true);

        var request = Create(context);

        request.ReadEntityBody(new byte[1], 1).ShouldBe(0);
        request.ReadEntityBody(new byte[1], 1).ShouldBe(0);
        Should.Throw<System.Web.HttpException>(
            () => request.BeginRead(new byte[1], 0, 1, null!, null!));
    }

    // A reset can surface before RequestAborted; classification must not depend on which won.
    [Fact]
    public async Task A_Reset_Is_Eof_Even_Before_Request_Aborted_Is_Raised()
    {
        var pipe = new Pipe();
        var context = BodyContext(pipe.Reader, contentLength: null);
        context.RequestAborted = CancellationToken.None;
        await pipe.Writer.CompleteAsync(new ConnectionResetException("reset by peer"));

        var request = Create(context);

        request.ReadEntityBody(new byte[1], 1).ShouldBe(0);
        Should.Throw<System.Web.HttpException>(
                () => request.BeginRead(new byte[1], 0, 1, null!, null!))
            .InnerException.ShouldBeOfType<ConnectionResetException>();
    }

    [Fact]
    public async Task A_Host_Rejected_Body_Throws_With_The_Host_Status_Code()
    {
        var pipe = new Pipe();
        var context = BodyContext(pipe.Reader, contentLength: null);
        await pipe.Writer.CompleteAsync(
            new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

        var request = Create(context);

        Should.Throw<System.Web.HttpException>(() => request.ReadEntityBody(new byte[1], 1))
            .GetHttpCode()
            .ShouldBe(StatusCodes.Status413PayloadTooLarge);
        Should.Throw<System.Web.HttpException>(
                () => request.BeginRead(new byte[1], 0, 1, null!, null!))
            .GetHttpCode()
            .ShouldBe(StatusCodes.Status413PayloadTooLarge);
    }

    [Fact]
    public void Apm_Zero_Count_Completes_Synchronously_Without_Consuming_The_Body()
    {
        var request = Create(BodyContext("body"));
        var buffer = new byte[4];

        var empty = request.BeginRead(buffer, 0, 0, null!, "empty");

        empty.CompletedSynchronously.ShouldBeTrue();
        request.EndRead(empty).ShouldBe(0);
        request.ReadEntityBody(buffer, 4).ShouldBe(4);
    }

    [Fact]
    public void Apm_Arguments_And_Result_Ownership_Are_Validated()
    {
        var first = Create(BodyContext("first"));
        var second = Create(BodyContext("second"));

        Should.Throw<ArgumentNullException>(
            () => first.BeginRead(null!, 0, 1, null!, null!));
        Should.Throw<ArgumentOutOfRangeException>(
            () => first.BeginRead(new byte[1], -1, 1, null!, null!));
        Should.Throw<ArgumentOutOfRangeException>(
            () => first.BeginRead(new byte[1], 0, -1, null!, null!));
        Should.Throw<ArgumentException>(
            () => first.BeginRead(new byte[1], 1, 1, null!, null!));
        Should.Throw<ArgumentNullException>(() => first.EndRead(null!));

        var result = first.BeginRead(new byte[1], 0, 1, null!, null!);
        Should.Throw<ArgumentException>(() => second.EndRead(result));
        first.EndRead(result).ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => first.EndRead(result));
    }

    [Fact]
    public async Task End_Of_Request_Cancels_A_Pending_Read()
    {
        var pipe = new Pipe();
        var request = Create(BodyContext(pipe.Reader, contentLength: null));
        var completed = new TaskCompletionSource<IAsyncResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var result = request.BeginRead(
            new byte[1],
            0,
            1,
            read => completed.SetResult(read),
            null!);

        request.EndOfRequest();

        await completed.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        Should.Throw<System.Web.HttpException>(() => request.EndRead(result));
        await pipe.Writer.CompleteAsync();
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

    private static DefaultHttpContext BodyContext(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var context = Context();
        context.Request.Method = "POST";
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(true));
        return context;
    }

    private static DefaultHttpContext BodyContext(PipeReader reader, long? contentLength)
    {
        var context = Context();
        context.Request.Method = "POST";
        context.Request.ContentLength = contentLength;
        context.Features.Set<IRequestBodyPipeFeature>(new RequestBodyPipeFeature(reader));
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(true));
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

    private sealed class BodyDetectionFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; } = canHaveBody;
    }

    private sealed class RequestBodyPipeFeature(PipeReader reader) : IRequestBodyPipeFeature
    {
        public PipeReader Reader { get; } = reader;
    }

    private sealed class AsyncOnlyBodyStream(byte[] body) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => body.Length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException("Synchronous request-body I/O was attempted.");

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var count = Math.Min(buffer.Length, body.Length - _position);
            body.AsMemory(_position, count).CopyTo(buffer);
            _position += count;
            return ValueTask.FromResult(count);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
