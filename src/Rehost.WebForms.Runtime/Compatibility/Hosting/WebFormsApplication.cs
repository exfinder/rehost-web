namespace Rehost.WebForms.Hosting;

using System;

public sealed class WebFormsApplicationOptions
{
    public const string DefaultMachineConfigurationFileName = "rehost-webforms.machine.config";
    public const string DefaultRootWebConfigurationFileName = "rehost-webforms.web.config";

    // Each variable substitutes the whole machineKey attribute string, so a value may carry the
    // isolation suffixes. Set both or neither. Set only where the declared attribute
    // auto-generates; an explicit configured key alongside a set variable fails preflight.
    public const string MachineKeyValidationKeyVariable = "REHOST_WEBFORMS_MACHINEKEY_VALIDATIONKEY";
    public const string MachineKeyDecryptionKeyVariable = "REHOST_WEBFORMS_MACHINEKEY_DECRYPTIONKEY";

    public string ApplicationId { get; set; }

    public string PhysicalRootPath { get; set; }

    public string VirtualRootPath { get; set; }

    public string MachineConfigurationFilePath { get; set; }

    public string RootWebConfigurationFilePath { get; set; }

    public string CompilationTempDirectory { get; set; }

    public string MachineKeyDirectory { get; set; }
}

public static class WebFormsApplication
{
    private static readonly ApplicationBootstrap Bootstrap = new(new ProcessApplicationBootstrapEnvironment());

    public static void Initialize(WebFormsApplicationOptions options)
    {
        Bootstrap.Initialize(options);
    }

    internal static ApplicationBootstrapConfiguration InitializedOrNull =>
        Bootstrap.State == ApplicationBootstrapState.Initialized ? Bootstrap.Configuration : null;

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
