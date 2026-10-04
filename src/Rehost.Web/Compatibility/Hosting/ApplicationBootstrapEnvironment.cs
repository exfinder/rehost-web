namespace Rehost.Web.Hosting;

using System;
using System.Collections.Generic;
using System.Data.Common;

internal interface IApplicationBootstrapEnvironment
{
    string HostDirectory { get; }

    IReadOnlyList<string> Preflight(ApplicationBootstrapConfiguration configuration);

    void Bind(ApplicationBootstrapConfiguration configuration);
}

internal sealed class ProcessApplicationBootstrapEnvironment : IApplicationBootstrapEnvironment
{
    public string HostDirectory => Hosting.HostDirectory.Path;

    public IReadOnlyList<string> Preflight(ApplicationBootstrapConfiguration configuration)
    {
        return ApplicationConfigurationPreflight.Validate(configuration);
    }

    public void Bind(ApplicationBootstrapConfiguration configuration)
    {
        ApplicationBinding.Bind(configuration, new AppDomainApplicationData());
        DbProviderFactoriesSection.Register(configuration.OpenMappedConfiguration());
    }
}
