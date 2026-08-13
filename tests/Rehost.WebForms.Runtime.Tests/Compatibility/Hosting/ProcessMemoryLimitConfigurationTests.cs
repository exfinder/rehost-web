using System.Web.Hosting;
using Shouldly;
using Xunit;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

// Framework took this limit from the hosting worker process, which cannot exist here. The value now
// comes from <processModel memoryLimit>, and 37 is used because the shipped default of 80 is what a
// build that never read the configuration would report.
public sealed class ProcessMemoryLimitConfigurationTests
{
    private const int ConfiguredPercent = 37;

    [Fact]
    public void The_Configured_Percentage_Reaches_The_Effective_Limit()
    {
        var trace = Run(ConfiguredPercent);

        var reported = trace
            .Where(entry => entry.StartsWith("private-bytes-limit:", StringComparison.Ordinal))
            .Select(entry => long.Parse(entry.Substring("private-bytes-limit:".Length)))
            .ShouldHaveSingleItem();

        var totalLimit = RuntimeMemorySampler.Sample().TotalLimitBytes;

        reported.ShouldBe(MemoryLimits.ComputeTrimLimit(ConfiguredPercent, totalLimit));
        reported.ShouldNotBe(MemoryLimits.ComputeTrimLimit(80, totalLimit));
    }

    private static List<string> Run(int memoryLimitPercent)
    {
        var root = Directory.CreateTempSubdirectory("rehost-memory-limit-");
        try
        {
            var tracePath = Path.Combine(root.FullName, "trace.txt");
            var temp = Path.Combine(root.FullName, "temp");
            Directory.CreateDirectory(temp);

            var machineConfig = Path.Combine(root.FullName, "machine.config");
            var shipped = Path.Combine(
                ScenarioHostInvocation.HostDirectory, "configs", "rehost-webforms.machine.config");
            var text = File.ReadAllText(shipped);
            text.ShouldContain("memoryLimit=\"80\"", Case.Sensitive);
            File.WriteAllText(
                machineConfig,
                text.Replace("memoryLimit=\"80\"", $"memoryLimit=\"{memoryLimitPercent}\"", StringComparison.Ordinal));
            // The IIS baseline is resolved beside the machine config and is required.
            File.Copy(
                Path.Combine(
                    ScenarioHostInvocation.HostDirectory,
                    "configs",
                    "rehost-webforms.applicationHost.config"),
                Path.Combine(root.FullName, "rehost-webforms.applicationHost.config"));

            using var process = new ScenarioHostInvocation()
                .Application(ScenarioHostInvocation.FixturePath("modern-target"))
                .CompilationTemp(temp)
                .Trace(tracePath)
                .MachineConfig(machineConfig)
                .Request("/quirks")
                .Start();
            process.WaitForExit();

            process.ExitCode.ShouldBe(0, process.StandardError);

            return TraceFile.ReadLines(tracePath);
        }
        finally
        {
            try
            {
                root.Delete(recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

}
