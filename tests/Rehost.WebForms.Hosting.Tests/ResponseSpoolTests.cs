using Microsoft.AspNetCore.Http;
using Rehost.WebForms.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

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
