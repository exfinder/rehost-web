using System.Reflection;
using System.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Hosting;

public sealed class RecycleLimitMonitorTests
{
    [Fact]
    public void Samples_Current_Process_Private_Bytes_Without_Native_AspNet()
    {
        var type = typeof(RecycleLimitMonitor).GetNestedType(
            "RecycleLimitMonitorSingleton",
            BindingFlags.Public)!;
        var constructor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(long) },
            modifiers: null)!;
        var monitor = constructor.Invoke(new object[] { long.MaxValue });

        try
        {
            type.GetMethod(
                    "PBytesMonitorThread",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(monitor, new object?[] { null });

            var index = (int)type.GetField(
                    "_idx",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(monitor)!;
            var samples = (long[])type.GetField(
                    "_samples",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(monitor)!;
            var expected = Environment.WorkingSet;

            samples[index].ShouldBeGreaterThan(expected / 2);
            samples[index].ShouldBeLessThan(expected * 2);
        }
        finally
        {
            type.GetMethod("Dispose")!.Invoke(monitor, null);
        }
    }
}
