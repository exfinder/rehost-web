using System.Diagnostics;
using System.Web.Util;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Util;

// Counter reached kernel32!QueryPerformanceCounter through SafeNativeMethods, which carries no
// platform guard, so page tracing threw DllNotFoundException off Windows.
public sealed class CounterTests
{
    [Fact]
    public void The_Counter_Measures_Elapsed_Time()
    {
        var frequency = Counter.Frequency;
        frequency.ShouldBeGreaterThan(0);

        var start = Counter.Value;
        Thread.Sleep(50);
        var elapsedSeconds = (double)(Counter.Value - start) / frequency;

        // One-sided: a loaded machine may sleep far longer, but never meaningfully less.
        elapsedSeconds.ShouldBeGreaterThan(0.04);
        elapsedSeconds.ShouldBeLessThan(30.0);
    }

    [Fact]
    public void The_Counter_Shares_The_Clock_Stopwatch_Reports()
    {
        Counter.Frequency.ShouldBe(Stopwatch.Frequency);
    }
}
