using System.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

public sealed class MemorySamplerTests
{
    // Framework read private bytes from webengine4.dll. The type carrying that entry point refuses
    // to initialize here, so reaching it at all ends the process from a timer thread.
    [Fact]
    public void Sampling_Reports_A_Process_Footprint_And_A_Total_Limit()
    {
        var reading = RuntimeMemorySampler.Sample();

        reading.ProcessFootprintBytes.ShouldBeGreaterThan(0);
        reading.TotalLimitBytes.ShouldBeGreaterThan(0);
        reading.HighLoadThresholdBytes.ShouldBeGreaterThan(0);
    }

    // Process.PrivateMemorySize64 reads zero on macOS and virtual address space on Linux, either of
    // which would leave the monitor unable to observe growth.
    [Fact]
    public void The_Process_Footprint_Tracks_Memory_The_Process_Actually_Holds()
    {
        var before = RuntimeMemorySampler.Sample().ProcessFootprintBytes;

        var held = new byte[64 * 1024 * 1024];
        for (var offset = 0; offset < held.Length; offset += 4096)
        {
            held[offset] = 1;
        }

        var after = RuntimeMemorySampler.Sample().ProcessFootprintBytes;
        GC.KeepAlive(held);

        after.ShouldBeGreaterThan(before + (32 * 1024 * 1024), $"before={before} after={after}");
    }

    [Fact]
    public void The_Total_Limit_Exceeds_The_Threshold_The_Collector_Acts_On()
    {
        var reading = RuntimeMemorySampler.Sample();

        reading.TotalLimitBytes.ShouldBeGreaterThan(reading.HighLoadThresholdBytes);
    }
}
