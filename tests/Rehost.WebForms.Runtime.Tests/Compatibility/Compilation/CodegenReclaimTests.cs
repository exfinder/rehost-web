using Shouldly;
using Xunit;
using static Rehost.WebForms.Runtime.Tests.Compatibility.Compilation.ScenarioTrace;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

public sealed class CodegenReclaimTests
{
    [Fact]
    public void Reclaims_An_Invalidated_Assembly_As_The_Platform_Allows()
    {
        using var application = ScenarioApplication.Create();
        application.Run();
        var stale = Directory.GetFiles(application.Segment, "App_Code.*.dll").Single();

        // The first process keeps its generated assemblies loaded while the second invalidates
        // them, which is the only way to reach the branch that cannot delete a file.
        using var gate = ScenarioGate.Take();
        var holding = application.StartRun(gate.Name);
        ScenarioApplication.WaitForEntry(application.TracePath, "holding", TimeSpan.FromSeconds(60));

        using var editor = application.CloneApplicationSharingCodegenRoot();
        editor.EditAppCode();
        editor.Run();

        gate.Release();
        holding.WaitForExit();
        holding.ExitCode.ShouldBe(0, holding.StandardError);

        IsLogicallyDeleted(stale).ShouldBeTrue();
    }
}
