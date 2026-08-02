using System.Security.Cryptography;
using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

public sealed class RequestBodyOverKestrelTests
{
    [Fact]
    public void Fixed_And_Chunked_Bodies_Reach_All_Raw_Request_Surfaces()
    {
        var probes = new[]
        {
            "fixed-input",
            "fixed-binary",
            "fixed-buffered",
            "fixed-bufferless",
            "chunked-bufferless",
            "chunked-apm",
            "expect-continue",
        };
        using var run = ScenarioRun.ServeBody("body", probes);

        for (var i = 0; i < probes.Length; i++)
        {
            run.Trace.ShouldContain("request:" + probes[i] + ":200");
            run.ResponseText(i).ShouldBe(Describe("body:" + probes[i]));
        }

        run.Trace.ShouldContain("expect-interim:100");
    }

    [Fact]
    public void Buffered_Input_Spills_Above_The_Configured_Threshold()
    {
        using var run = ScenarioRun.ServeBody("body", "spill");

        run.Trace.ShouldContain("request:spill:200");
        run.Trace.ShouldContain("x-spilled:True");
        run.ResponseText(0).ShouldBe(Describe(new string('s', 2048)));
    }

    [Fact]
    public void SystemWeb_Rejects_A_Body_Above_MaxRequestLength()
    {
        using var run = ScenarioRun.ServeBody("body", "too-large");

        run.Trace.ShouldContain("request:too-large:500");
        run.ResponseText(0).ShouldContain("Maximum request length exceeded");
    }

    [Fact]
    public void SystemWeb_Rejects_An_Unknown_Length_Body_Above_MaxRequestLength()
    {
        using var run = ScenarioRun.ServeBody("body", "chunked-too-large");

        run.Trace.ShouldContain("request:chunked-too-large:500");
        run.ResponseText(0).ShouldContain("Maximum request length exceeded");
    }

    [Fact]
    public void Async_Preload_Buffers_A_Delayed_Chunked_Body_Before_The_Handler()
    {
        using var run = ScenarioRun.ServeBody("body-preload", "preload-delayed");

        run.Trace.ShouldContain("request:preload-delayed:200");
        run.Trace.ShouldContain("x-read-mode:Buffered");
        run.ResponseText(0).ShouldBe(Describe("body:preload-delayed"));
    }

    [Fact]
    public void Kestrel_Drains_An_Unread_Body_Before_The_Next_Request_On_The_Connection()
    {
        using var run = ScenarioRun.ServeBody("body", "unread", "fixed-input");

        run.ResponseText(0).ShouldBe("unread");
        run.ResponseText(1).ShouldBe(Describe("body:fixed-input"));
        run.Trace.Where(line => line.StartsWith("x-remote-port:", StringComparison.Ordinal))
            .Distinct()
            .Count()
            .ShouldBe(1);
    }

    [Fact]
    public void A_Client_Abort_During_A_Synchronous_Bufferless_Read_Becomes_HttpException()
    {
        using var run = ScenarioRun.ServeBody("body", "abort");

        run.Trace.ShouldContain("body-abort-interim:100");
        run.Trace.ShouldContain("body-abort:System.Web.HttpException");
        run.Trace.ShouldContain("request:abort:client-closed");
    }

    [Fact]
    public void A_Client_Abort_During_A_Worker_Apm_Read_Becomes_HttpException()
    {
        using var run = ScenarioRun.ServeBody("body", "abort-apm");

        run.Trace.ShouldContain("body-apm-abort-interim:100");
        run.Trace.ShouldContain("body-apm-abort:System.Web.HttpException");
        run.Trace.ShouldContain("request:abort-apm:client-closed");
    }

    [Fact]
    public void Kestrel_Refuses_A_Declared_Length_Over_Its_Own_Limit_Before_The_Pipeline()
    {
        using var run = ScenarioRun.ServeBody("body", "kestrel-too-large");

        run.Trace.ShouldContain("request:kestrel-too-large:413");
        run.Trace.ShouldNotContain("handler-entered:input");

        // Empty content type: System.Web never ran. An app-rendered rejection carries one.
        run.Trace.ShouldContain("content-type:");
    }

    [Fact]
    public void An_Undeclared_Length_Over_The_Host_Limit_Fails_The_Handler_Mid_Read()
    {
        using var run = ScenarioRun.ServeBody("body", "chunked-kestrel-too-large");

        run.Trace.ShouldContain("handler-entered:input");
        run.Trace.ShouldContain("request:chunked-kestrel-too-large:413");
    }

    // An ordinary HttpException by the time System.Web sees it, so the app's error config wins.
    [Fact]
    public void Custom_Errors_Convert_A_Host_Rejection_Into_The_Application_Response()
    {
        using var run = ScenarioRun.ServeBody(
            "body-customerrors",
            "chunked-customerrors-kestrel-too-large");

        run.Trace.ShouldContain("handler-entered:input");
        run.Trace.ShouldContain("request:chunked-customerrors-kestrel-too-large:302");
    }

    private static string Describe(string value)
    {
        var body = Encoding.UTF8.GetBytes(value);
        return body.Length + ":" + Convert.ToHexString(SHA256.HashData(body));
    }
}
