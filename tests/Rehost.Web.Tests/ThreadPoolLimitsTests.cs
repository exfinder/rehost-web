using Rehost.Web.Hosting;
using Rehost.Web.ScenarioProtocol;
using Rehost.Web.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests;

// Guards the seam P95 introduced: HttpRuntime reaches the pool through ThreadPool.SetMaxThreads
// where Framework used a webengine4 export. The counts are per CPU and deliberately not the shipped
// ones, so a build that ignored <processModel> cannot pass. A conflicting machine config is what
// costs this claim its own activation.
public sealed class ThreadPoolLimitsTests
{
    private const string ShippedProcessModel = """<processModel autoConfig="true" memoryLimit="80" />""";

    private const string ConfiguredProcessModel =
        """
        <processModel autoConfig="false" maxWorkerThreads="37" maxIoThreads="41"
                      minWorkerThreads="3" minIoThreads="5" memoryLimit="80" />
        """;

    [Fact]
    public void Configured_Thread_Counts_Reach_The_Pool()
    {
        var cpus = Environment.ProcessorCount;

        var reading = Run(ConfiguredProcessModel);

        reading.MaxWorker.ShouldBe(37 * cpus);
        reading.MaxIo.ShouldBe(41 * cpus);
        reading.MinWorker.ShouldBe(3 * cpus);
        reading.MinIo.ShouldBe(5 * cpus);
    }

    private sealed record Reading(int MaxWorker, int MaxIo, int MinWorker, int MinIo);

    private static Reading Run(string processModel)
    {
        using var staged = StagedApplication.Stage("modern-target");

        var shipped = Path.Combine(
            ScenarioHostInvocation.HostDirectory,
            "configs",
            RehostWebOptions.DefaultMachineConfigurationFileName);
        var text = File.ReadAllText(shipped);
        text.ShouldContain(ShippedProcessModel, Case.Sensitive);

        var machineConfig = Path.Combine(staged.RootPath, "machine.config");
        File.WriteAllText(machineConfig, text.Replace(ShippedProcessModel, processModel, StringComparison.Ordinal));
        // The IIS baseline is resolved beside the machine config and is required.
        File.Copy(
            Path.Combine(ScenarioHostInvocation.HostDirectory, "configs", "rehost.applicationHost.config"),
            Path.Combine(staged.RootPath, "rehost.applicationHost.config"));

        using var process = staged.Batch()
            .MachineConfig(machineConfig)
            .Request(ProbePaths.Quirks)
            .Start();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, process.StandardError);

        var recorded = staged.Trace()
            .Where(entry => entry.StartsWith("thread-pool:", StringComparison.Ordinal))
            .ShouldHaveSingleItem();
        var parts = recorded.Substring("thread-pool:".Length).Split(':');
        parts.Length.ShouldBe(5);

        return new Reading(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
    }
}
