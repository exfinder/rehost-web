using System.Configuration;

namespace System.Web.Services.Configuration;

public sealed class WebServicesSection : ConfigurationSection
{
    [ConfigurationProperty("protocols")]
    public ProtocolElementCollection Protocols => (ProtocolElementCollection)this["protocols"];

    public WebServiceProtocols EnabledProtocols
    {
        get
        {
            WebServiceProtocols enabled = WebServiceProtocols.Unknown;
            foreach (ProtocolElement protocol in Protocols)
            {
                enabled |= protocol.Name;
            }
            return enabled;
        }
    }
}
