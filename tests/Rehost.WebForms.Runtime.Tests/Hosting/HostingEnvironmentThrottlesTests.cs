using System.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Hosting;

// Framework's message named integrated pipeline mode, which ADR 0013 now says this host is in,
// so the refusal must not name it again (ledger P93).
public sealed class HostingEnvironmentThrottlesTests
{
    private static void ShouldRefuseNaming(string property, Action accessor)
    {
        var failure = Should.Throw<PlatformNotSupportedException>(accessor);

        failure.Message.ShouldContain($"HostingEnvironment.{property}", Case.Sensitive);
        failure.Message.ShouldNotContain("requires IIS integrated pipeline mode");
    }

    [Fact]
    public void Reading_MaxConcurrentRequestsPerCPU_Refuses() =>
        ShouldRefuseNaming(
            nameof(HostingEnvironment.MaxConcurrentRequestsPerCPU),
            () => _ = HostingEnvironment.MaxConcurrentRequestsPerCPU);

    [Fact]
    public void Writing_MaxConcurrentRequestsPerCPU_Refuses() =>
        ShouldRefuseNaming(
            nameof(HostingEnvironment.MaxConcurrentRequestsPerCPU),
            () => HostingEnvironment.MaxConcurrentRequestsPerCPU = 8);

    [Fact]
    public void Reading_MaxConcurrentThreadsPerCPU_Refuses() =>
        ShouldRefuseNaming(
            nameof(HostingEnvironment.MaxConcurrentThreadsPerCPU),
            () => _ = HostingEnvironment.MaxConcurrentThreadsPerCPU);

    [Fact]
    public void Writing_MaxConcurrentThreadsPerCPU_Refuses() =>
        ShouldRefuseNaming(
            nameof(HostingEnvironment.MaxConcurrentThreadsPerCPU),
            () => HostingEnvironment.MaxConcurrentThreadsPerCPU = 8);
}
