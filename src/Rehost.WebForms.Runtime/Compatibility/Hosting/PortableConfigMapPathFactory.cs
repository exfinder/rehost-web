namespace Rehost.WebForms.Hosting;

using System.Web.Configuration;

internal sealed class PortableConfigMapPathFactory : IConfigMapPathFactory
{
    private readonly ApplicationBootstrapConfiguration _configuration;

    internal PortableConfigMapPathFactory(ApplicationBootstrapConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IConfigMapPath Create(string virtualPath, string physicalPath)
    {
        return new UserMapPath(_configuration.CreateFileMap());
    }
}
