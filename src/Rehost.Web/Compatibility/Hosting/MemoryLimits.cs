#nullable enable

namespace System.Web.Hosting;

using System;
using System.Collections.Generic;
using System.Globalization;

internal static class MemoryLimits
{
    internal const long ReserveBytes = 256L * 1024 * 1024;

    private const int DefaultHighMemoryPercent = 90;

    private static int s_highMemoryPercent;

    // A container limit reduces TotalAvailableMemoryBytes to a fraction of itself while
    // MemoryLoadBytes stays measured against the whole, so their ratio overstates load.
    // HighMemoryLoadThresholdBytes shares MemoryLoadBytes' basis and its percentage is readable.
    internal static long ResolveTotalLimit(long highLoadThresholdBytes)
    {
        if (highLoadThresholdBytes <= 0)
        {
            return 0;
        }

        return highLoadThresholdBytes * 100 / HighMemoryPercent;
    }

    internal static int HighMemoryPercent
    {
        get
        {
            var percent = s_highMemoryPercent;
            if (percent == 0)
            {
                percent = ReadHighMemoryPercent();
                s_highMemoryPercent = percent;
            }

            return percent;
        }
    }

    // Trimming and compacting need room that does not grow with the container, so a percentage
    // alone leaves too little of it on a small one.
    internal static long ComputeTrimLimit(int percent, long totalLimitBytes)
    {
        if (totalLimitBytes <= 0 || percent <= 0)
        {
            return 0;
        }

        if (percent > 100)
        {
            percent = 100;
        }

        // The reserve exceeds a very small container outright, and a limit of zero disables the
        // monitor. Flooring it here rather than the result leaves a configured share below half
        // still honoured.
        var byReserve = Math.Max(totalLimitBytes - ReserveBytes, totalLimitBytes / 2);

        return Math.Min(totalLimitBytes / 100 * percent, byReserve);
    }

    internal static int ComputeLoadPercent(long loadBytes, long totalLimitBytes)
    {
        if (loadBytes <= 0 || totalLimitBytes <= 0)
        {
            return 0;
        }

        return (int)(loadBytes * 100 / totalLimitBytes);
    }

    private static int ReadHighMemoryPercent()
    {
        try
        {
            IReadOnlyDictionary<string, object> variables = GC.GetConfigurationVariables();
            if (variables.TryGetValue("GCHighMemPercent", out var value) && value is not null)
            {
                var percent = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                if (percent > 0 && percent <= 100)
                {
                    return percent;
                }
            }
        }
        catch (Exception)
        {
        }

        return DefaultHighMemoryPercent;
    }
}
