namespace System.Web.Hosting;

using System;
using System.IO;
using System.Web.Util;

internal static class CurrentAppDomainHosting
{
    private const string SecondaryAppDomainsMessage =
        "Secondary AppDomains are unavailable. Rehost supports one Web Forms application per process.";

    private const string ClientBuildManagerMessage =
        "ClientBuildManager is unavailable. Precompilation must run outside the Rehost runtime process.";

    private static readonly object BindingLock = new object();

    internal static PlatformNotSupportedException SecondaryAppDomainsUnsupported() =>
        new PlatformNotSupportedException(SecondaryAppDomainsMessage);

    internal static PlatformNotSupportedException ClientBuildManagerUnsupported() =>
        new PlatformNotSupportedException(ClientBuildManagerMessage);

    internal static HostingEnvironment CreateHostingEnvironment(
        ApplicationManager applicationManager,
        string appId,
        IApplicationHost appHost,
        HostingEnvironmentParameters hostingParameters)
    {
        if (hostingParameters != null &&
            (hostingParameters.HostingFlags & HostingEnvironmentFlags.ClientBuildManager) != 0)
        {
            throw new PlatformNotSupportedException(ClientBuildManagerMessage);
        }

        var physicalPath = appHost.GetPhysicalPath();
        if (!StringUtil.StringEndsWith(physicalPath, Path.DirectorySeparatorChar))
        {
            physicalPath += Path.DirectorySeparatorChar;
        }

        var virtualPath = VirtualPath.Create(appHost.GetVirtualPath()).VirtualPathString;
        BindOrValidateApplication(appId, physicalPath, virtualPath);

        var environment = new HostingEnvironment();
        environment.Initialize(
            applicationManager,
            appHost,
            appHost.GetConfigMapPathFactory(),
            hostingParameters,
            policyLevel: null);
        return environment;
    }

    private static void BindOrValidateApplication(string appId, string physicalPath, string virtualPath)
    {
        lock (BindingLock)
        {
            var domain = AppDomain.CurrentDomain;
            if (domain.GetData(".appDomain") != null)
            {
                ValidateBinding(domain, ".appId", appId);
                ValidateBinding(
                    domain,
                    ".appPath",
                    physicalPath,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal);
                ValidateBinding(domain, ".appVPath", virtualPath);
                return;
            }

            domain.SetData(".appId", appId);
            domain.SetData(".appPath", physicalPath);
            domain.SetData(".appVPath", virtualPath);
            domain.SetData(".domainId", appId + "-current");
            domain.SetData(".appDomain", "*");
        }
    }

    private static void ValidateBinding(
        AppDomain domain,
        string key,
        string expected,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        var actual = domain.GetData(key) as string;
        if (!String.Equals(actual, expected, comparison))
        {
            throw new InvalidOperationException(
                "The process is already bound to a different Web Forms application.");
        }
    }
}
