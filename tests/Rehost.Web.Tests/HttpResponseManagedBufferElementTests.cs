using System.Text;
using System.Web;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests;

// Every failure this class can produce is silent: no exception, no response difference. The
// parity gate exercises one element holding nine bytes, so capacity, reuse, and the copy-out
// paths are only reachable here.
public sealed class HttpResponseManagedBufferElementTests
{
    [Fact]
    public void A_New_Element_Accepts_Writes()
    {
        var element = new HttpResponseManagedBufferElement();

        element.FreeBytes.ShouldBeGreaterThan(0);
        element.Append(new byte[] { 1, 2, 3 }, 0, 3).ShouldBe(3);
    }

    [Fact]
    public void Capacity_Comes_From_The_Rented_Array_Rather_Than_The_Requested_Size()
    {
        var element = new HttpResponseManagedBufferElement(100);
        var capacity = element.FreeBytes;

        capacity.ShouldBeGreaterThanOrEqualTo(100);
        element.Append(new byte[capacity], 0, capacity).ShouldBe(capacity);
    }

    [Fact]
    public void Send_Surrenders_A_Copy_Sized_To_The_Content_Rather_Than_The_Rented_Array()
    {
        var element = new HttpResponseManagedBufferElement();
        var capacity = element.FreeBytes;
        element.Append(Encoding.ASCII.GetBytes("oracle-ok"), 0, 9);

        var worker = new CapturingWorkerRequest();
        ((IHttpResponseElement)element).Send(worker);

        worker.Sent.Count.ShouldBe(1);
        worker.Sent[0].ShouldBe(Encoding.ASCII.GetBytes("oracle-ok"));
        worker.SurrenderedArrayLengths[0].ShouldBe(9);
        worker.SurrenderedArrayLengths[0].ShouldNotBe(capacity);
    }

    [Fact]
    public void GetBytes_Returns_A_Content_Sized_Copy_And_Null_While_Empty()
    {
        var element = new HttpResponseManagedBufferElement();
        ((IHttpResponseElement)element).GetBytes().ShouldBeNull();

        element.Append(new byte[] { 7, 8 }, 0, 2);

        ((IHttpResponseElement)element).GetBytes().ShouldBe(new byte[] { 7, 8 });
    }

    // HttpWriter.BufferData loops on the returned count to spill into further elements.
    [Fact]
    public void Append_Returns_The_Accepted_Count_And_Zero_Once_Full()
    {
        var element = new HttpResponseManagedBufferElement();
        var capacity = element.FreeBytes;
        var oversized = new byte[capacity + 512];

        element.Append(oversized, 0, oversized.Length).ShouldBe(capacity);
        element.FreeBytes.ShouldBe(0);
        element.Append(oversized, 0, 1).ShouldBe(0);
    }

    // Returning one array to ArrayPool twice hands it to two renters at once, which is the
    // corruption class this element exists to avoid.
    [Fact]
    public void Recycle_Returns_The_Rented_Array_At_Most_Once()
    {
        var element = new HttpResponseManagedBufferElement();
        element.Append(new byte[] { 1 }, 0, 1);

        element.Recycle();
        Should.NotThrow(() => element.Recycle());

        var reused = new HttpResponseManagedBufferElement();
        var alsoReused = new HttpResponseManagedBufferElement();
        reused.Append(new byte[] { 42 }, 0, 1);
        alsoReused.Append(new byte[] { 99 }, 0, 1);

        ((IHttpResponseElement)reused).GetBytes().ShouldBe(new byte[] { 42 });
        ((IHttpResponseElement)alsoReused).GetBytes().ShouldBe(new byte[] { 99 });
    }

    [Fact]
    public void Clone_Is_Independent_Of_The_Original_And_Outlives_Its_Recycling()
    {
        var element = new HttpResponseManagedBufferElement();
        element.Append(Encoding.ASCII.GetBytes("snapshot"), 0, 8);

        var clone = element.Clone();
        element.Recycle();

        ((IHttpResponseElement)clone).GetSize().ShouldBe(8);
        ((IHttpResponseElement)clone).GetBytes().ShouldBe(Encoding.ASCII.GetBytes("snapshot"));
    }

    private sealed class CapturingWorkerRequest : HttpWorkerRequest
    {
        internal List<byte[]> Sent { get; } = new();

        // The array as handed over, not a copy of its first `length` bytes: an implementation
        // that surrendered the rented buffer would otherwise be indistinguishable here.
        internal List<int> SurrenderedArrayLengths { get; } = new();

        public override void SendResponseFromMemory(byte[] data, int length)
        {
            SurrenderedArrayLengths.Add(data.Length);

            var captured = new byte[length];
            Buffer.BlockCopy(data, 0, captured, 0, length);
            Sent.Add(captured);
        }

        public override string GetUriPath() => "/";

        public override string GetQueryString() => "";

        public override string GetRawUrl() => "/";

        public override string GetHttpVerbName() => "GET";

        public override string GetHttpVersion() => "HTTP/1.1";

        public override string GetRemoteAddress() => "127.0.0.1";

        public override int GetRemotePort() => 0;

        public override string GetLocalAddress() => "127.0.0.1";

        public override int GetLocalPort() => 0;

        public override void SendStatus(int statusCode, string statusDescription)
        {
        }

        public override void SendKnownResponseHeader(int index, string value)
        {
        }

        public override void SendUnknownResponseHeader(string name, string value)
        {
        }

        public override void SendResponseFromFile(string filename, long offset, long length)
        {
        }

        public override void SendResponseFromFile(IntPtr handle, long offset, long length)
        {
        }

        public override void FlushResponse(bool finalFlush)
        {
        }

        public override void EndOfRequest()
        {
        }
    }
}
