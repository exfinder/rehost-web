using Shouldly;
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

        Value(second, "app-code:").ShouldNotBe(Value(first, "app-code:"));
        Value(second, "resource:").ShouldBe("neutral-greeting");
        IsLogicallyDeleted(Path.Combine(
            application.Segment,
            Path.GetFileName(Value(first, "app-code:")) + ".dll")).ShouldBeTrue();
    }
}
