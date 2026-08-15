using System.Diagnostics;
using Microsoft.Owin.Host.SystemWeb.CallEnvironment;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Owin.Host.SystemWeb.Tests.CallEnvironment;

public sealed class TraceTextWriterTests
{
    // Upstream wrote through kernel32!OutputDebugString, which is a DllNotFoundException off
    // Windows the first time the OWIN pipeline touches host.TraceOutput.
    [Fact]
    public void WritingReachesAManagedTraceListener()
    {
        Debugger.IsLogging().ShouldBeFalse("the managed sink is only reached when no debugger is logging");

        var listener = new CapturingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            TraceTextWriter.Instance.Write("owin-trace");
            TraceTextWriter.Instance.WriteLine("-line");
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        listener.Captured.ShouldBe("owin-trace-line" + Environment.NewLine);
    }
}
