using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

public sealed class CodegenSharedSegmentTests
{
    [Fact]
    public void Serves_A_Second_Process_Sharing_One_Codegen_Segment()
    {
        using var first = BatchApplication.Create();
        using var second = first.CloneApplicationSharingCodegenRoot();

        // Both processes compile the same application from one segment at once, which is the
        // condition the cross-process compilation mutex exists for.
        using var gate = HoldGate.Take();
        var firstRun = first.StartRun(gate.Name);
        BatchApplication.WaitForEntry(first.TracePath, "holding", TimeSpan.FromSeconds(60), firstRun);

        var secondTrace = second.Run();

        gate.Release();
        firstRun.WaitForExit();
        firstRun.ExitCode.ShouldBe(0, firstRun.StandardError);

        secondTrace.ShouldContain("request:/default:200");
        TraceChannel.ReadLines(first.TracePath).ShouldContain("request:/default:200");
    }
}
