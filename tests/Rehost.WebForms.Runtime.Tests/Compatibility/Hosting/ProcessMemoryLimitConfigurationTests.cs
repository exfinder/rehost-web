using Rehost.WebForms.ScenarioProtocol;
using System.Web.Hosting;
using Shouldly;
using Rehost.WebForms.Hosting;
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
        using var staged = StagedApplication.Stage("modern-target");

        var machineConfig = Path.Combine(staged.RootPath, "machine.config");
        var shipped = Path.Combine(
            ScenarioHostInvocation.HostDirectory, "configs", WebFormsApplicationOptions.DefaultMachineConfigurationFileName);
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
            Path.Combine(staged.RootPath, "rehost-webforms.applicationHost.config"));

        using var process = staged.Batch()
            .MachineConfig(machineConfig)
            .Request(ProbePaths.Quirks)
            .Start();
        process.WaitForExit();

        process.ExitCode.ShouldBe(0, process.StandardError);

        return staged.Trace();
    }

}
