using Shouldly;
using Xunit;
using static Rehost.WebForms.Runtime.Tests.Compatibility.Compilation.ScenarioTrace;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

// The first run is the shared substrate fixture's; only the second run is paid for here.
[Collection(nameof(CodegenSubstrateCollection))]
public sealed class CodegenReuseTests(CodegenSubstrateFixture fixture)
{
    [Fact]
    public void Reuses_The_Previous_Run_Output_When_Nothing_Changed()
    {
        var application = fixture.Application;
        var firstAssemblies = Directory.GetFiles(application.Segment, "*.dll").Order().ToArray();

        var second = application.Run();

        // A recompile draws a new random assembly name, so identical names mean the second run
        // loaded the first run's output instead of rebuilding it.
        Value(second, "app-code:").ShouldBe(Value(fixture.FirstTrace, "app-code:"));
        Value(second, "sub-code:").ShouldBe(Value(fixture.FirstTrace, "sub-code:"));
        Directory.GetFiles(application.Segment, "*.dll").Order().ShouldBe(firstAssemblies);
    }
}
