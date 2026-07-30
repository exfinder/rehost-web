namespace Rehost.WebForms.Hosting;

using System;

public sealed class WebFormsApplicationOptions
{
    public string ApplicationId { get; set; }

    public string PhysicalRootPath { get; set; }

    public string VirtualRootPath { get; set; }

    public string MachineConfigurationFilePath { get; set; }

    public string RootWebConfigurationFilePath { get; set; }

    public string CompilationTempDirectory { get; set; }
}

public static class WebFormsApplication
{
    private static readonly ApplicationBootstrap Bootstrap = new(new ProcessApplicationBootstrapEnvironment());

    public static void Initialize(WebFormsApplicationOptions options)
    {
        Bootstrap.Initialize(options);
    }

    internal static ApplicationBootstrapConfiguration RequireInitialized()
    {
        var configuration = Bootstrap.Configuration;
        if (Bootstrap.State != ApplicationBootstrapState.Initialized || configuration == null)
        {
            throw new InvalidOperationException(
                "WebFormsApplication.Initialize must complete before creating the System.Web hosting environment.");
        }

        return configuration;
    }
}
