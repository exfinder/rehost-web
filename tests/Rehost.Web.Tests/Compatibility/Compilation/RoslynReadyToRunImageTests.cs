using System.Reflection.PortableExecutable;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.Compilation;

// Guards the eng/RoslynReadyToRun.targets substitution: every failure mode of that
// infrastructure degrades silently to IL, which costs ~450 ms of compiler JIT per process
// but fails no functional test. An IL assembly has no managed native header, so this turns
// the silent regression into a red test.
public sealed class RoslynReadyToRunImageTests
{
    [Theory]
    [InlineData("Microsoft.CodeAnalysis.dll")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.dll")]
    public void Roslyn_Assembly_In_The_Test_Output_Is_A_ReadyToRun_Image(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);

        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);

        reader.PEHeaders.CorHeader.ShouldNotBeNull();
        reader.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size.ShouldBeGreaterThan(
            0,
            $"{fileName} carries no ReadyToRun header, so the substitution reverted to IL");
    }
}
