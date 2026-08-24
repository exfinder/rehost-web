#nullable enable

namespace System.Web.Hosting;

using System;

internal readonly struct MemoryReading
{
    internal MemoryReading(long totalLimitBytes, long loadBytes, long highLoadThresholdBytes, long processFootprintBytes)
    {
        TotalLimitBytes = totalLimitBytes;
        LoadBytes = loadBytes;
        HighLoadThresholdBytes = highLoadThresholdBytes;
        ProcessFootprintBytes = processFootprintBytes;
    }

    // Physical RAM, or the container's limit when one is imposed.
    internal long TotalLimitBytes { get; }

    // Zero until the first collection.
    internal long LoadBytes { get; }

    internal long HighLoadThresholdBytes { get; }

    internal long ProcessFootprintBytes { get; }
}

internal static class RuntimeMemorySampler
{
    internal static MemoryReading Sample()
    {
        var info = GC.GetGCMemoryInfo();

        return new MemoryReading(
            MemoryLimits.ResolveTotalLimit(info.HighMemoryLoadThresholdBytes),
            info.MemoryLoadBytes,
            info.HighMemoryLoadThresholdBytes,
            Environment.WorkingSet);
    }
}
