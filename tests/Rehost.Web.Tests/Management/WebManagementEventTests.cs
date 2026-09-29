using System.Web.Management;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Management;

public sealed class WebManagementEventTests
{
    private sealed class ProbeEvent : WebManagementEvent
    {
        public ProbeEvent() : base("probe", null, WebEventCodes.WebExtendedBase + 1)
        {
        }
    }

    // The process information behind every management event named the worker through a kernel32
    // call; off Windows that made the first event a TypeInitializationException, swallowed by the
    // runtime error raise and thrown to any application raising its own.
    [Fact]
    public void Process_Information_Names_The_Current_Process()
    {
        var information = new ProbeEvent().ProcessInformation;

        information.ProcessName.ShouldBe(Path.GetFileName(Environment.ProcessPath));
        information.ProcessID.ShouldBe(Environment.ProcessId);
    }
}
