using Rehost.Owin.Host.SystemWeb.Tests.Loader;

[assembly: Microsoft.Owin.OwinStartup(ScannedStartup.FriendlyName, typeof(ScannedStartup))]

namespace Rehost.Owin.Host.SystemWeb.Tests.Loader;

public sealed class ScannedStartup
{
    public const string FriendlyName = "ScannerProbe";

    public const string RanKey = "rehost.ScannedStartupRan";

    public void Configuration(global::Owin.IAppBuilder app) => app.Properties[RanKey] = true;
}
