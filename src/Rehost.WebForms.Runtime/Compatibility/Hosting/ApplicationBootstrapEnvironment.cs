namespace Rehost.WebForms.Hosting;

using System;
using System.Data.Common;

internal interface IApplicationBootstrapEnvironment
{
    string HostDirectory { get; }

    void Preflight(ApplicationBootstrapConfiguration configuration);

    void Bind(ApplicationBootstrapConfiguration configuration);
}

internal sealed class ProcessApplicationBootstrapEnvironment : IApplicationBootstrapEnvironment
{
    public string HostDirectory => Hosting.HostDirectory.Path;

    public void Preflight(ApplicationBootstrapConfiguration configuration)
    {
        ApplicationConfigurationPreflight.Validate(configuration);
    }

    public void Bind(ApplicationBootstrapConfiguration configuration)
    {
        ApplicationBinding.Bind(configuration, new AppDomainApplicationData());
        DbProviderFactoriesSection.Register(configuration.OpenMappedConfiguration());
    }
}
