using System.Web.Util;
using Microsoft.Extensions.Logging.Abstractions;
using Rehost.WebForms.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Diagnostics;

[Collection(nameof(ApplicationBootstrapCollection))]
public sealed class RuntimeDiagnosticsIntakeTests : IDisposable
{
    public void Dispose()
    {
        RuntimeDiagnostics.ResetForTests();
    }

    [Fact]
    public void The_Options_Factory_Is_Published_Before_Preflight_Runs()
    {
        using var application = TemporaryApplication.Create();
        var factory = new CollectingLoggerFactory();
        var options = application.CreateOptions();
        options.LoggerFactory = factory;
        var bootstrap = new ApplicationBootstrap(new PreflightingEnvironment(application.OutputDirectory));

        bootstrap.Initialize(options);

        var entry = factory.Entries.ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(7);
        entry.Message.ShouldContain("validationKey and decryptionKey");
        entry.Message.ShouldContain(application.MachineKeyDirectory);
    }

    [Fact]
    public void Bootstrap_Without_A_Factory_Leaves_The_Channel_Silent()
    {
        using var application = TemporaryApplication.Create();
        var bootstrap = new ApplicationBootstrap(new PreflightingEnvironment(application.OutputDirectory));

        bootstrap.Initialize(application.CreateOptions());

        bootstrap.State.ShouldBe(ApplicationBootstrapState.Initialized);
        RuntimeDiagnostics.Logger.ShouldBeSameAs(NullLogger.Instance);
    }

    [Fact]
    public void The_Test_Reset_Detaches_An_Earlier_Factory()
    {
        var earlier = new CollectingLoggerFactory();
        RuntimeDiagnostics.Publish(earlier);
        RuntimeDiagnostics.ResetForTests();
        var later = new CollectingLoggerFactory();
        RuntimeDiagnostics.Publish(later);

        WebFormsRuntimeEventSource.Log.MonitorDisabled("RecycleLimitMonitor", new IOException("sampling failed"));

        later.Entries.ShouldHaveSingleItem().Message.ShouldContain("RecycleLimitMonitor");
        earlier.Entries.ShouldBeEmpty();
    }

    private sealed class PreflightingEnvironment(string baseDirectory) : IApplicationBootstrapEnvironment
    {
        public string BaseDirectory { get; } = baseDirectory;

        public void Preflight(ApplicationBootstrapConfiguration configuration)
        {
            ApplicationConfigurationPreflight.Validate(configuration);
        }

        public void Bind(ApplicationBootstrapConfiguration configuration)
        {
        }
    }
}
