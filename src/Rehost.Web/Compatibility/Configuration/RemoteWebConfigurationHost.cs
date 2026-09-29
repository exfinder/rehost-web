using System;
using System.Configuration.Internal;

namespace System.Web.Configuration;

internal sealed class RemoteWebConfigurationHost : DelegatingConfigHost
{
    internal const string UnsupportedMessage = "Remote web configuration is not supported by Rehost.Web.";

    internal RemoteWebConfigurationHost() => throw new PlatformNotSupportedException(UnsupportedMessage);

    public override void Init(IInternalConfigRoot configRoot, params object[] hostInitParams) =>
        throw new PlatformNotSupportedException(UnsupportedMessage);

    public override void InitForConfiguration(
        ref string locationSubPath,
        out string configPath,
        out string locationConfigPath,
        IInternalConfigRoot root,
        params object[] hostInitConfigurationParams) =>
        throw new PlatformNotSupportedException(UnsupportedMessage);
}
