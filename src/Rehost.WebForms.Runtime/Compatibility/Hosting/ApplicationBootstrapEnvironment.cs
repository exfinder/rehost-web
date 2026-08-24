namespace Rehost.WebForms.Hosting;

using System;

internal interface IApplicationBootstrapEnvironment
{
    string BaseDirectory { get; }

    void Preflight(ApplicationBootstrapConfiguration configuration);

    void Bind(ApplicationBootstrapConfiguration configuration);
}

internal sealed class ProcessApplicationBootstrapEnvironment : IApplicationBootstrapEnvironment
{
    public string BaseDirectory => AppContext.BaseDirectory;

    public void Preflight(ApplicationBootstrapConfiguration configuration)
    {
        ApplicationConfigurationPreflight.Validate(configuration);
    }

    public void Bind(ApplicationBootstrapConfiguration configuration)
    {
        ApplicationBinding.Bind(configuration, new AppDomainApplicationData());
    }
}
