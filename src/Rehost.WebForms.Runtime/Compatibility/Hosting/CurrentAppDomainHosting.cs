namespace System.Web.Hosting;

using System;
using System.IO;
using Rehost.WebForms.Hosting;
using System.Web.Configuration;
using System.Web.Util;

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
        HostingEnvironmentParameters hostingParameters,
        Exception startupConfigurationException = null)
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

        var physicalPath = NormalizeHostPhysicalPath(appHost.GetPhysicalPath());
        var virtualPath = VirtualPath.Create(appHost.GetVirtualPath()).VirtualPathString;
        ValidateBinding(configuration, appId, physicalPath, virtualPath);
        HostDirectory.PublishBaseDirectory(configuration.PhysicalRootPath);
        WebFormsRuntimeEventSource.Log.PhysicalRoot(configuration.PhysicalRootPath);

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
        if (startupConfigurationException == null)
        {
            environment.Initialize(
                applicationManager,
                appHost,
                configuration.ConfigMapPathFactory,
                hostingParameters,
                policyLevel: null);
        }
        else
        {
            environment.Initialize(
                applicationManager,
                appHost,
                configuration.ConfigMapPathFactory,
                hostingParameters,
                policyLevel: null,
                startupConfigurationException);
        }

        SpendAppSettingsThrowOnce();
        return environment;
    }

    // AppSettings rethrows a broken configuration once, then latches; spent here, that throw lands
    // on the startup failure HostingInit latched, not on the flush of the request reporting it.
    private static void SpendAppSettingsThrowOnce()
    {
        try
        {
            _ = AppSettings.EnsureSessionStateLockedOnFlush;
        }
        catch (Exception exception)
        {
            WebFormsRuntimeEventSource.Log.SwallowedException(
                $"{nameof(CurrentAppDomainHosting)}.{nameof(SpendAppSettingsThrowOnce)}",
                exception,
                "AppSettings.EnsureSettingsLoaded");
        }
    }

    private static string NormalizeHostPhysicalPath(string physicalPath)
    {
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
