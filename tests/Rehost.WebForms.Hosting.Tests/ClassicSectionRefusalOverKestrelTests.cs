using Rehost.WebForms.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// MH5: IIS answered every request to such an application with 500.22/500.23, static files
// included, so the port refuses activation instead. There is no host to share — the process never
// reaches the point of serving.
public sealed class ClassicSectionRefusalOverKestrelTests
{
    [Fact]
    public void An_Unflagged_Classic_Registration_Refuses_Activation_Naming_The_Entries()
    {
        using var staged = StagedApplication.Stage(Fixtures.ClassicUnflagged.Name);

        using var host = staged.Serve().Start();
        host.WaitForExit();

        host.ExitCode.ShouldNotBe(0, host.StandardOutput);
        host.StandardError.ShouldContain("httpModules <add name=\"ClassicOnly\">");
        host.StandardError.ShouldContain("httpHandlers <add path=\"probe3.axd\">");
        host.StandardError.ShouldContain("validateIntegratedModeConfiguration=\"false\"");
    }
}
