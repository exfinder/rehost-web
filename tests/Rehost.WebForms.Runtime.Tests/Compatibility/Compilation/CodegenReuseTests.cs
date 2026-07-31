using Shouldly;
using Xunit;
using static Rehost.WebForms.Runtime.Tests.Compatibility.Compilation.ScenarioTrace;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

public sealed class CodegenReuseTests
{
    [Fact]
    public void Reuses_The_Previous_Run_Output_When_Nothing_Changed()
    {
        using var application = ScenarioApplication.Create();

        var first = application.Run();
        var firstAssemblies = Directory.GetFiles(application.Segment, "*.dll").Order().ToArray();

        var second = application.Run();

        // A recompile draws a new random assembly name, so identical names mean the second run
        // loaded the first run's output instead of rebuilding it.
        Value(second, "app-code:").ShouldBe(Value(first, "app-code:"));
        Value(second, "sub-code:").ShouldBe(Value(first, "sub-code:"));
        Directory.GetFiles(application.Segment, "*.dll").Order().ShouldBe(firstAssemblies);
    }
}
