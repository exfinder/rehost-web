namespace System.Web.Hosting;

using System;
using System.IO;
using Rehost.WebForms.Hosting;
using System.Web.Configuration;

internal static class CurrentAppDomainHosting
{
    private const string SecondaryAppDomainsMessage =
        "Secondary AppDomains are unavailable. Rehost supports one Web Forms application per process.";

    private const string ClientBuildManagerMessage =
        "ClientBuildManager is unavailable. Precompilation must run outside the Rehost runtime process.";

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

        if (hostingParameters != null && hostingParameters.IISExpressVersion != null)
        {
            throw new PlatformNotSupportedException(
                "IIS Express hosting is unavailable. Use the Rehost host integration.");
        }

        var configuration = WebFormsApplication.RequireInitialized();

        if (appHost.GetConfigToken() != IntPtr.Zero)
        {
            throw new PlatformNotSupportedException(
                "Native configuration access tokens are unavailable. Use explicit Rehost configuration paths.");
        }

        var physicalPath = NormalizeHostPhysicalPath(appHost.GetPhysicalPath(), configuration.PhysicalRootPath);
        var virtualPath = VirtualPath.Create(appHost.GetVirtualPath()).VirtualPathString;
        ValidateBinding(configuration, appId, physicalPath, virtualPath);

        hostingParameters ??= new HostingEnvironmentParameters();
        if (hostingParameters.FcnMode != FcnMode.NotSet &&
            hostingParameters.FcnMode != FcnMode.Disabled)
        {
            throw new PlatformNotSupportedException(
                "Configuration reload is unavailable. Use FcnMode.Disabled.");
        }

        hostingParameters.FcnMode = FcnMode.Disabled;
        hostingParameters.FcnSkipReadAndCacheDacls = true;

        var environment = new HostingEnvironment();
        environment.Initialize(
            applicationManager,
            appHost,
            configuration.ConfigMapPathFactory,
            hostingParameters,
            policyLevel: null);
        return environment;
    }

    private static string NormalizeHostPhysicalPath(string physicalPath, string configuredPhysicalPath)
    {
        if (!OperatingSystem.IsWindows() &&
            physicalPath.EndsWith('\\') &&
            String.Equals(
                physicalPath.Substring(0, physicalPath.Length - 1),
                configuredPhysicalPath.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.Ordinal))
        {
            return configuredPhysicalPath;
        }

        return Path.EndsInDirectorySeparator(physicalPath)
            ? physicalPath
            : physicalPath + Path.DirectorySeparatorChar;
    }

    private static void ValidateBinding(
        ApplicationBootstrapConfiguration configuration,
        string appId,
        string physicalPath,
        string virtualPath)
    {
        if (!String.Equals(configuration.ApplicationId, appId, StringComparison.OrdinalIgnoreCase) ||
            !String.Equals(
                configuration.PhysicalRootPath,
                physicalPath,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal) ||
            !String.Equals(
                configuration.VirtualRootPath,
                VirtualPath.Create(virtualPath).VirtualPathStringNoTrailingSlash,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The System.Web host values conflict with the initialized Web Forms application.");
        }
    }
}
