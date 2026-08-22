using Shouldly;
using Rehost.WebForms.Parity.Contracts;
using Xunit;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

// Framework published the application's configuration into AppDomain data from the AppDomain
// creation path. There is no child AppDomain here, so the portable path publishes it instead.
// UnobtrusiveValidationMode is the cheapest observable: it reads straight off BinaryCompatibility.
public sealed class ApplicationConfigurationPublicationTests
{
    [Fact]
    public void A_Declared_Target_Framework_Reaches_The_Quirks_Switch()
    {
        var trace = Run("modern-target");

        trace.ShouldContain("unobtrusive-validation:WebForms");
    }

    // Framework hands a startup configuration failure to HostingEnvironment.Initialize rather than
    // aborting, so activation completes and every request renders it.
    [Fact]
    public void An_Application_Declaring_No_Target_Framework_Is_Refused_Per_Request()
    {
        var trace = Run("legacy-target");

        trace.ShouldContain("request:/quirks:500");
        trace.ShouldContain(
            entry => entry.StartsWith(TraceEvents.ErrorBody) && entry.Contains("targetFramework"),
            "The rendered error should name the missing targetFramework.");
    }

    private static List<string> Run(string fixture)
    {
        var (exitCode, standardError, trace) = Start(fixture);

        exitCode.ShouldBe(0, standardError);
        return trace;
    }

    private static (int ExitCode, string StandardError, List<string> Trace) Start(string fixture)
    {
        using var staged = StagedApplication.Stage(fixture);

        using var process = staged.Invocation()
            .Request("/quirks")
            .Start();
        process.WaitForExit();

        return (process.ExitCode, process.StandardError, staged.Trace());
    }

}
