using Shouldly;
using Rehost.WebForms.Parity.Contracts;
using Xunit;
using static Rehost.WebForms.Runtime.Tests.Compatibility.Compilation.ScenarioTrace;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

public sealed class CodegenRecompileTests
{
    [Fact]
    public void Recompiles_After_Application_Code_Changes()
    {
        using var application = ScenarioApplication.Create();

        var first = application.Run();

        application.EditAppCode();
        var second = application.Run();

        Value(second, TraceEvents.AppCode).ShouldNotBe(Value(first, TraceEvents.AppCode));
        Value(second, TraceEvents.Resource).ShouldBe("neutral-greeting");
        IsLogicallyDeleted(Path.Combine(
            application.Segment,
            Path.GetFileName(Value(first, TraceEvents.AppCode)) + ".dll")).ShouldBeTrue();
    }
}
