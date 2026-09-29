using System.Collections;
using System.Web;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests;

// Response.Headers off an IIS7 worker request (ledger P68). Every claim is asserted on the
// generated header block or on what the worker request was told to send, not on the collection
// alone: a collection that stored entries and reached nothing would satisfy the reads.
public sealed class ResponseHeaderCollectionTests
{
    [Fact]
    public void The_Collection_Stores_Adds_Sets_And_Removes()
    {
        var response = NewResponse(out _);

        response.Headers.Add("X-Custom", "v1");
        response.Headers.Add("X-Custom", "v2");
        response.Headers.Add("X-Other", "o");

        response.Headers.GetValues("X-Custom").ShouldBe(new[] { "v1", "v2" });
        response.Headers.AllKeys.ShouldBe(new[] { "X-Custom", "X-Other" });

        response.Headers.Set("X-Custom", "only");
        response.Headers.Get("X-Custom").ShouldBe("only");

        response.Headers.Remove("X-Other");
        response.Headers.AllKeys.ShouldBe(new[] { "X-Custom" });
    }

    [Fact]
    public void An_Appended_Custom_Header_Shows_In_The_Collection_And_Reaches_The_Block()
    {
        var response = NewResponse(out _);

        response.AppendHeader("X-Custom", "v1");
        response.AppendHeader("X-Custom", "v2");

        response.Headers.GetValues("X-Custom").ShouldBe(new[] { "v1", "v2" });
        Generated(response).ShouldBe("X-Custom: v1|X-Custom: v2|Cache-Control: private|Content-Type: text/html");
    }

    [Fact]
    public void Removing_An_Appended_Header_Drops_It_From_The_Block()
    {
        var response = NewResponse(out _);
        response.AppendHeader("X-Custom", "v1");

        response.Headers.Remove("X-Custom");

        response.Headers.AllKeys.ShouldBeEmpty();
        Generated(response).ShouldBe("Cache-Control: private|Content-Type: text/html");
    }

    [Fact]
    public void The_Field_Apis_Leave_The_Collection_Empty()
    {
        var response = NewResponse(out _);

        response.ContentType = "text/plain";
        response.RedirectLocation = "/x";
        response.Cache.SetCacheability(HttpCacheability.Public);
        response.Cache.SetMaxAge(TimeSpan.FromSeconds(30));

        response.Headers.Count.ShouldBe(0);
        Generated(response).ShouldBe("Location: /x|Cache-Control: public, max-age=30|Content-Type: text/plain");
    }

    [Fact]
    public void A_Collection_Content_Type_Neither_Moves_The_Field_Nor_Reaches_The_Block()
    {
        var response = NewResponse(out _);

        response.Headers.Set("Content-Type", "text/csv");

        response.Headers.Get("Content-Type").ShouldBe("text/csv");
        response.ContentType.ShouldBe("text/html");
        Generated(response).ShouldBe("Cache-Control: private|Content-Type: text/html");
    }

    [Fact]
    public void A_Collection_Cache_Control_Loses_To_The_Generated_One()
    {
        var response = NewResponse(out _);

        response.Headers.Set("Cache-Control", "no-store");

        Generated(response).ShouldBe("Cache-Control: private|Content-Type: text/html");
    }

    [Fact]
    public void A_Collection_Location_Is_Sent_While_RedirectLocation_Is_Null()
    {
        var response = NewResponse(out _);

        response.Headers.Set("Location", "/y");

        response.RedirectLocation.ShouldBeNull();
        Generated(response).ShouldBe("Location: /y|Cache-Control: private|Content-Type: text/html");
    }

    [Fact]
    public void RedirectLocation_Wins_Over_A_Collection_Location()
    {
        var response = NewResponse(out _);

        response.Headers.Set("Location", "/y");
        response.RedirectLocation = "/x";

        Generated(response).ShouldBe("Location: /x|Cache-Control: private|Content-Type: text/html");
    }

    // Cookie generation needs the configuration an unactivated process has none of; the
    // interleaving with Response.Cookies is a scenario claim (readings H10, H14).
    [Fact]
    public void A_Collection_Set_Cookie_Reaches_The_Block()
    {
        var response = NewResponse(out _);

        response.Headers.Add("Set-Cookie", "z=9; path=/");

        Generated(response).ShouldBe(
            "Set-Cookie: z=9; path=/|Cache-Control: private|Content-Type: text/html");
    }

    [Fact]
    public void Adding_After_The_Block_Left_Throws_The_Framework_Message()
    {
        var response = NewResponse(out var worker);
        response.Write("body");
        response.Flush();
        worker.SentHeaders.ShouldNotBeEmpty();

        var thrown = Should.Throw<HttpException>(() => response.Headers.Add("X-Late", "v"));

        thrown.Message.ShouldBe("Server cannot append header after HTTP headers have been sent.");
    }

    [Fact]
    public void Removing_After_The_Block_Left_Is_Inert()
    {
        var response = NewResponse(out var worker);
        response.AppendHeader("X-Custom", "v1");
        response.Write("body");
        response.Flush();

        Should.NotThrow(() => response.Headers.Remove("X-Custom"));

        worker.SentHeaders.ShouldContain("X-Custom: v1");
        response.Headers.Get("X-Custom").ShouldBe("v1");
    }

    [Fact]
    public void After_The_Block_Left_The_Collection_Reports_It()
    {
        var response = NewResponse(out _);
        response.Write("body");
        response.Flush();

        response.Headers.Get("Cache-Control").ShouldBe("private");
        response.Headers.Get("Content-Type").ShouldBe("text/html; charset=utf-8");
    }

    [Fact]
    public void ClearHeaders_Empties_The_Collection()
    {
        var response = NewResponse(out _);
        response.AppendHeader("X-Custom", "v1");

        response.ClearHeaders();

        response.Headers.Count.ShouldBe(0);
        Generated(response).ShouldBe("Cache-Control: private|Content-Type: text/html");
    }

    // IV7: the request collection is writable too, Add appending comma-joined, and only Clear
    // stays refused.
    [Fact]
    public void Request_Headers_Take_Adds_Sets_And_Removes()
    {
        var headers = new HttpContext(new RecordingWorkerRequest()).Request.Headers;

        headers.Set("X-Custom", "v1");
        headers.Set("X-Custom", "v2");
        headers.Add("X-Multi", "a");
        headers.Add("X-Multi", "b");
        headers.Add("X-Gone", "g");
        headers.Remove("X-Gone");

        headers["X-Custom"].ShouldBe("v2");
        headers["X-Multi"].ShouldBe("a,b");
        headers["X-Gone"].ShouldBeNull();
        Should.Throw<NotSupportedException>(() => headers.Clear());
    }

    private static HttpResponse NewResponse(out RecordingWorkerRequest worker)
    {
        worker = new RecordingWorkerRequest();
        return new HttpContext(worker).Response;
    }

    private static string Generated(HttpResponse response) =>
        string.Join(
            "|",
            response.GenerateResponseHeaders(false)
                .Cast<HttpResponseHeader>()
                .Select(header => header.Name + ": " + header.Value));

    private sealed class RecordingWorkerRequest : HttpWorkerRequest
    {
        internal List<string> SentHeaders { get; } = new();

        public override void SendKnownResponseHeader(int index, string value) =>
            SentHeaders.Add(GetKnownResponseHeaderName(index) + ": " + value);

        public override void SendUnknownResponseHeader(string name, string value) =>
            SentHeaders.Add(name + ": " + value);

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

        public override void SendResponseFromMemory(byte[] data, int length)
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
