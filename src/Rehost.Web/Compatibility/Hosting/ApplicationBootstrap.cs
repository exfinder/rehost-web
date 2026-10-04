namespace Rehost.Web.Hosting;

using System;
using System.Threading;

internal enum ApplicationBootstrapState
{
    Uninitialized,
    Initializing,
    Initialized,
    Faulted,
}

internal sealed class ApplicationBootstrap
{
    private readonly IApplicationBootstrapEnvironment _environment;
    private int _state;
    private ApplicationBootstrapConfiguration _configuration;

    internal ApplicationBootstrap(IApplicationBootstrapEnvironment environment)
    {
        _environment = environment;
    }

    internal ApplicationBootstrapState State => (ApplicationBootstrapState)Volatile.Read(ref _state);

    internal ApplicationBootstrapConfiguration Configuration => Volatile.Read(ref _configuration);

    internal void Initialize(RehostWebOptions options)
    {
        var priorState = Interlocked.CompareExchange(
            ref _state,
            (int)ApplicationBootstrapState.Initializing,
            (int)ApplicationBootstrapState.Uninitialized);
        if (priorState != (int)ApplicationBootstrapState.Uninitialized)
        {
            throw new InvalidOperationException(
                "RehostWebApplication.Initialize must be called exactly once per process.");
        }

        try
        {
            var configuration = ApplicationBootstrapConfiguration.Create(options, _environment.HostDirectory);
            System.Web.Util.RehostWebLogger.Publish(configuration.LoggerFactory);
            var serverConfiguration = System.Web.IisConfig.IisServerConfiguration.Load(
                configuration.ServerConfigurationFilePath,
                configuration.ApplicationConfigurationFilePath,
                configuration.VirtualRootPath);
            serverConfiguration.ReportUnsupported(configuration.ApplicationConfigurationFilePath);
            var probingDirectories = _environment.Preflight(configuration);
            System.Web.Util.GeneratedAssemblyLoader.PublishProbingDirectories(probingDirectories);
            _environment.Bind(configuration);
            System.Web.Configuration.HttpConfigurationSystem.SetConfigurationFilePaths(
                configuration.MachineConfigurationFilePath,
                configuration.RootWebConfigurationFilePath);
            System.Web.IisConfig.IisServerConfiguration.Publish(serverConfiguration);
            Volatile.Write(ref _configuration, configuration);
            Volatile.Write(ref _state, (int)ApplicationBootstrapState.Initialized);
        }
        catch
        {
            Volatile.Write(ref _state, (int)ApplicationBootstrapState.Faulted);
            throw;
        }
    }
}
