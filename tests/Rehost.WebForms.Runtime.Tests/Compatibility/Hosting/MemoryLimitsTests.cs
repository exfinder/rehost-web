using System.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

// The arithmetic below only diverges from Framework's once a container limit exists, which neither
// supported platform imposes on itself. Synthetic readings are the only way to reach those shapes
// without one; see docs/follow-ups/container-memory-validation.md.
public sealed class MemoryLimitsTests
{
    private const long MiB = 1024 * 1024;

    // The collector reports its high-memory threshold as a percentage of the true limit.
    private static long ThresholdFor(long totalLimitBytes) =>
        totalLimitBytes / 100 * MemoryLimits.HighMemoryPercent;

    [Theory]
    [InlineData(512 * MiB)]
    [InlineData(1024 * MiB)]
    [InlineData(4096 * MiB)]
    [InlineData(16384 * MiB)]
    public void The_Total_Limit_Is_Recovered_From_The_High_Memory_Threshold(long totalLimitBytes)
    {
        var resolved = MemoryLimits.ResolveTotalLimit(ThresholdFor(totalLimitBytes));

        // Integer division loses at most one part in the percentage.
        resolved.ShouldBeInRange(totalLimitBytes - 128, totalLimitBytes);
    }

    [Fact]
    public void An_Absent_Threshold_Yields_No_Total_Limit()
    {
        MemoryLimits.ResolveTotalLimit(0).ShouldBe(0);
        MemoryLimits.ResolveTotalLimit(-1).ShouldBe(0);
    }

    [Theory]
    // The reserve binds below roughly 1.25 GiB, the percentage above it.
    [InlineData(512, 80, 256)]
    [InlineData(1024, 80, 768)]
    [InlineData(4096, 80, 3276)]
    [InlineData(8192, 80, 6553)]
    [InlineData(4096, 37, 1515)]
    [InlineData(1024, 60, 614)]
    public void The_Trim_Limit_Takes_The_Percentage_Or_The_Reserve(int totalMiB, int percent, int expectedMiB)
    {
        var limit = MemoryLimits.ComputeTrimLimit(percent, totalMiB * MiB);

        (limit / MiB).ShouldBe(expectedMiB);
    }

    [Fact]
    public void A_Container_Too_Small_For_The_Reserve_Keeps_Half_Of_It()
    {
        // Subtracting the reserve outright would be negative here, which disables the monitor.
        MemoryLimits.ComputeTrimLimit(80, 256 * MiB).ShouldBe(128 * MiB);
        MemoryLimits.ComputeTrimLimit(80, 384 * MiB).ShouldBe(192 * MiB);
    }

    [Fact]
    public void No_Limit_Is_Imposed_Without_A_Total_Or_A_Percentage()
    {
        MemoryLimits.ComputeTrimLimit(80, 0).ShouldBe(0);
        MemoryLimits.ComputeTrimLimit(0, 1024 * MiB).ShouldBe(0);
    }

    [Fact]
    public void A_Percentage_Above_The_Whole_Is_Clamped()
    {
        MemoryLimits.ComputeTrimLimit(400, 8192 * MiB)
            .ShouldBe(MemoryLimits.ComputeTrimLimit(100, 8192 * MiB));
    }

    // The defect this replaces divided MemoryLoadBytes by TotalAvailableMemoryBytes. In a container
    // the numerator is measured against the whole limit and the denominator is three quarters of
    // it, so a true 70% was reported as 93% and a true 75% as 100%.
    [Theory]
    [InlineData(70)]
    [InlineData(75)]
    [InlineData(90)]
    public void Load_Is_Measured_Against_The_Whole_Limit_Not_The_Heap_Ceiling(int truePercent)
    {
        // A round decimal total keeps the percentages exact rather than truncated.
        const long totalLimit = 1_000_000_000;
        var load = totalLimit / 100 * truePercent;
        var heapCeiling = totalLimit / 4 * 3;

        MemoryLimits.ComputeLoadPercent(load, totalLimit).ShouldBe(truePercent);
        MemoryLimits.ComputeLoadPercent(load, heapCeiling).ShouldBeGreaterThan(truePercent);
    }

    [Fact]
    public void Load_Before_The_First_Collection_Reads_As_None()
    {
        MemoryLimits.ComputeLoadPercent(0, 1024 * MiB).ShouldBe(0);
        MemoryLimits.ComputeLoadPercent(512 * MiB, 0).ShouldBe(0);
    }

}
