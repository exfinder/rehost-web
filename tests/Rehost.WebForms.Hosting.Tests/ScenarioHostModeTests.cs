using Rehost.WebForms.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

public sealed class ScenarioHostModeTests
{
    [Fact]
    public void Serve_Mode_Refuses_A_Batch_Only_Option()
    {
        using var host = new ScenarioHostInvocation()
            .Serve()
            .HoldGate(@"Local\rehost-mode-guard-test")
            .Start();
        host.WaitForExit();

        host.ExitCode.ShouldBe(1);
        host.StandardError.ShouldContain("--hold-gate is not honored in --serve mode");
    }

    [Fact]
    public void Batch_Mode_Refuses_A_Serve_Only_Option()
    {
        using var host = new ScenarioHostInvocation()
            .Http2()
            .Start();
        host.WaitForExit();

        host.ExitCode.ShouldBe(1);
        host.StandardError.ShouldContain("--http2 is not honored in batch mode");
    }
}
