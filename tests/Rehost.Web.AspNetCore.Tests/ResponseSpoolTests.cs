using System.Text;
using Microsoft.AspNetCore.Http;
using Rehost.Web.AspNetCore;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// The spool keeps a body in memory to a threshold and spills the rest to a file under the
// temporary directory; the file must be gone once the request is over, whether the body was
// delivered, the client went away before the commit, or the commit itself failed.
public sealed class ResponseSpoolTests
{
    private const int Threshold = 16;

    [Fact]
    public async Task A_Body_Past_The_Threshold_Spills_To_Disk_Is_Delivered_And_Is_Cleaned_Up()
    {
        var temp = Directory.CreateTempSubdirectory("rehost-spool-");
        try
        {
            var body = new byte[Threshold * 8];
            Random.Shared.NextBytes(body);
            var delivered = new MemoryStream();
            var context = new DefaultHttpContext();
            context.Response.Body = delivered;

            using (var spool = new ResponseSpool(() => temp.FullName, Threshold))
            {
                spool.Write(body, body.Length);
                temp.GetFiles().ShouldNotBeEmpty();

                spool.Seal();
                await spool.CommitAsync(context, CancellationToken.None);
            }

            delivered.ToArray().ShouldBe(body);
            temp.GetFiles().ShouldBeEmpty();
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_Spilled_Body_Never_Committed_Is_Cleaned_Up_On_Dispose()
    {
        var temp = Directory.CreateTempSubdirectory("rehost-spool-");
        try
        {
            using (var spool = new ResponseSpool(() => temp.FullName, Threshold))
            {
                spool.Write(new byte[Threshold * 4], Threshold * 4);
                temp.GetFiles().ShouldNotBeEmpty();
            }

            temp.GetFiles().ShouldBeEmpty();
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task A_Failing_Commit_Still_Cleans_Up_The_Spill()
    {
        var temp = Directory.CreateTempSubdirectory("rehost-spool-");
        try
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new FailingStream();

            using (var spool = new ResponseSpool(() => temp.FullName, Threshold))
            {
                spool.Write(new byte[Threshold * 4], Threshold * 4);
                spool.Seal();

                await Should.ThrowAsync<IOException>(
                    () => spool.CommitAsync(context, CancellationToken.None));
                temp.GetFiles().ShouldNotBeEmpty();
            }

            temp.GetFiles().ShouldBeEmpty();
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task A_Body_Under_The_Threshold_Never_Touches_Disk()
    {
        var temp = Directory.CreateTempSubdirectory("rehost-spool-");
        try
        {
            var delivered = new MemoryStream();
            var context = new DefaultHttpContext();
            context.Response.Body = delivered;
            var body = new byte[Threshold - 1];

            using (var spool = new ResponseSpool(() => temp.FullName, Threshold))
            {
                spool.Write(body, body.Length);
                temp.GetFiles().ShouldBeEmpty();
                spool.Seal();
                await spool.CommitAsync(context, CancellationToken.None);
            }

            delivered.Length.ShouldBe(body.Length);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    // A flush that fails on the transport marks the spool faulted so the terminal commit does not
    // re-enter the half-drained segment (the mid-stream-disconnect path).
    [Fact]
    public async Task A_Faulted_Delivery_Makes_Later_Commits_No_Op_Instead_Of_Re_Draining()
    {
        var temp = Directory.CreateTempSubdirectory("rehost-spool-");
        try
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new FailingStream();
            using var spool = new ResponseSpool(() => temp.FullName, Threshold);

            spool.Write(new byte[Threshold * 4], Threshold * 4);
            await Should.ThrowAsync<IOException>(() => spool.CommitAsync(context, CancellationToken.None));
            spool.DeliveryFaulted.ShouldBeTrue();

            // A second commit (the middleware's terminal one) must not touch the stream again.
            var deliveredAfter = new MemoryStream();
            var terminal = new DefaultHttpContext();
            terminal.Response.Body = deliveredAfter;
            await Should.NotThrowAsync(() => spool.CommitAsync(terminal, CancellationToken.None));
            deliveredAfter.Length.ShouldBe(0);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task A_Swapped_Entity_Replaces_The_Spilled_Body_And_Deletes_Its_File()
    {
        var temp = Directory.CreateTempSubdirectory("rehost-spool-");
        try
        {
            var delivered = new MemoryStream();
            var context = new DefaultHttpContext();
            context.Response.Body = delivered;

            using (var spool = new ResponseSpool(() => temp.FullName, Threshold))
            {
                spool.Write(new byte[Threshold * 8], Threshold * 8);
                temp.GetFiles().ShouldNotBeEmpty();

                spool.Seal();
                spool.ReplaceEntity("text/plain", "swapped"u8.ToArray());
                temp.GetFiles().ShouldBeEmpty();

                await spool.CommitAsync(context, TestContext.Current.CancellationToken);
            }

            Encoding.ASCII.GetString(delivered.ToArray()).ShouldBe("swapped");
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    // IIS replaced the entity alone: the status and the headers its own refusal set stay on the
    // custom page, and only the type and the length follow the new bytes.
    [Fact]
    public void A_Swap_Keeps_The_Status_And_Headers_And_Replaces_Only_The_Type_And_Length()
    {
        using var spool = new ResponseSpool(Path.GetTempPath, Threshold);
        spool.SetStatus(405, "Method Not Allowed");
        spool.AddHeader("Connection", "close");
        spool.AddHeader("Content-Type", "text/html");
        spool.AddHeader("Allow", "GET, HEAD");
        spool.SetContentLength(9);
        spool.Write("no method"u8.ToArray(), 9);
        spool.Seal();

        spool.ReplaceEntity("application/json", """{"no":1}"""u8.ToArray());

        spool.StatusCode.ShouldBe(405);
        spool.ReasonPhrase.ShouldBe("Method Not Allowed");
        spool.Headers.Select(header => $"{header.Name}={header.Value}").ShouldBe(
            ["Connection=close", "Allow=GET, HEAD", "Content-Type=application/json"]);
        spool.ContentLength.ShouldBe(8);
    }

    [Fact]
    public async Task A_Swap_After_The_Head_Was_Committed_Is_Refused()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var spool = new ResponseSpool(Path.GetTempPath, Threshold);
        spool.Write("first"u8.ToArray(), 5);

        await spool.FlushAsync(context, TestContext.Current.CancellationToken);

        Should.Throw<InvalidOperationException>(
                () => spool.ReplaceEntity("text/plain", "late"u8.ToArray()))
            .Message.ShouldContain("head is still open");
    }

    // The swap happens once, when the head leaves: what is buffered then is replaced and what the
    // application writes afterwards flows through, which is chunked framing, not a declared length.
    [Fact]
    public async Task The_Head_Commit_Hook_Runs_Once_And_Later_Writes_Follow_The_Swap()
    {
        var delivered = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Response.Body = delivered;
        var hooks = 0;
        using var spool = new ResponseSpool(Path.GetTempPath, Threshold);
        spool.BeforeHeadCommit = (swapped, _) =>
        {
            hooks++;
            swapped.ReplaceEntity("application/json", "[replaced]"u8.ToArray());
        };

        spool.SetStatus(404, "Not Found");
        spool.Write("buffered"u8.ToArray(), 8);
        await spool.FlushAsync(context, TestContext.Current.CancellationToken);

        Encoding.ASCII.GetString(delivered.ToArray()).ShouldBe("[replaced]");
        spool.ContentLength.ShouldBeNull();

        spool.Write("|later"u8.ToArray(), 6);
        spool.Seal();
        await spool.CommitAsync(context, TestContext.Current.CancellationToken);

        hooks.ShouldBe(1);
        Encoding.ASCII.GetString(delivered.ToArray()).ShouldBe("[replaced]|later");
    }

    private sealed class FailingStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("client gone");
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new IOException("client gone");
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("client gone");
    }
}
