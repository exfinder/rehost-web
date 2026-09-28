namespace Rehost.WebForms.Hosting;

using System;
using System.Configuration;
using System.Data.Common;
using System.IO;
using System.Runtime.Versioning;
using System.Web.Configuration;
using System.Web.Hosting;
using System.Web.SessionState;
using System.Web.Util;

internal static class ApplicationConfigurationPreflight
{
    internal static void Validate(ApplicationBootstrapConfiguration configuration)
    {
        ValidateRequiredFile(
            configuration.MachineConfigurationFilePath,
            "machine configuration");
        ValidateRequiredFile(
            configuration.RootWebConfigurationFilePath,
            "root web configuration");
        ValidateOptionalFile(
            configuration.ApplicationConfigurationFilePath,
            "application configuration");

        try
        {
            var mappedConfiguration = configuration.OpenMappedConfiguration();
            var httpRuntime = RequireSection<HttpRuntimeSection>(
                mappedConfiguration,
                "system.web/httpRuntime");
            var trust = RequireSection<TrustSection>(
                mappedConfiguration,
                "system.web/trust");
            var compilation = RequireSection<CompilationSection>(
                mappedConfiguration,
                "system.web/compilation");
            RequireSection<HostingEnvironmentSection>(
                mappedConfiguration,
                "system.web/hostingEnvironment");
            RequireSection<DbProviderFactoriesSection>(
                mappedConfiguration,
                DbProviderFactoriesSection.SectionName);

            if (httpRuntime.FcnMode != FcnMode.Disabled)
            {
                throw new ConfigurationErrorsException(
                    "Configuration reload is unavailable. Set system.web/httpRuntime fcnMode=\"Disabled\".");
            }

            if (!String.Equals(trust.Level, "Full", StringComparison.Ordinal) ||
                trust.LegacyCasModel)
            {
                throw new PlatformNotSupportedException(
                    "Rehost supports only trust level=\"Full\" with legacyCasModel=\"false\".");
            }

            var syncContextSetting = mappedConfiguration.AppSettings.Settings[
                "aspnet:UseTaskFriendlySynchronizationContext"]?.Value;
            if (Boolean.TryParse(syncContextSetting, out var taskFriendly) && !taskFriendly)
            {
                throw new PlatformNotSupportedException(
                    "The legacy ASP.NET synchronization context is unsupported. Remove " +
                    "appSettings key \"aspnet:UseTaskFriendlySynchronizationContext\" or set it " +
                    "to \"true\"; the rehosted runtime supports only the task-friendly context.");
            }

            ValidateSessionStateMode(RequireSection<SessionStateSection>(
                mappedConfiguration,
                "system.web/sessionState"));

            ValidateAuthenticationMode(RequireSection<AuthenticationSection>(
                mappedConfiguration,
                "system.web/authentication"));

            ValidateCompilationTargetFramework(compilation);

            ValidateCompilationTempDirectory(configuration, compilation);
            ValidateMachineKey(configuration, mappedConfiguration);
            ReportUnsupportedSystemNet(mappedConfiguration);
        }
        catch (ConfigurationErrorsException exception)
        {
            throw new ConfigurationErrorsException(
                $"Web Forms configuration preflight failed. " +
                $"Machine='{configuration.MachineConfigurationFilePath}', " +
                $"RootWeb='{configuration.RootWebConfigurationFilePath}', " +
                $"Application='{configuration.ApplicationConfigurationFilePath}'. " +
                exception.Message,
                exception,
                exception.Filename,
                exception.Line);
        }
    }

    private static void ReportUnsupportedSystemNet(Configuration mappedConfiguration)
    {
        if (mappedConfiguration.GetSection("system.net") is IgnoreSection net
            && net.SectionInformation.GetRawXml() != null)
        {
            WebFormsRuntimeEventSource.Log.SystemNetUnsupported(mappedConfiguration.FilePath);
        }
    }

    // Reads the declared key strings only. Resolving them would generate the keys, which is what
    // RuntimeDataInitialize does on first use.
    internal static void ValidateMachineKey(
        ApplicationBootstrapConfiguration configuration,
        Configuration mappedConfiguration)
    {
        if ((configuration.MachineKeyValidationKeyOverride == null) !=
            (configuration.MachineKeyDecryptionKeyOverride == null))
        {
            throw new ConfigurationErrorsException(
                $"The {MachineKeyEnvironmentOverrides.ValidationKeyVariable} and " +
                $"{MachineKeyEnvironmentOverrides.DecryptionKeyVariable} environment variables " +
                $"must be set together. " +
                (configuration.MachineKeyValidationKeyOverride == null
                    ? MachineKeyEnvironmentOverrides.ValidationKeyVariable
                    : MachineKeyEnvironmentOverrides.DecryptionKeyVariable) +
                " is missing.");
        }

        if (mappedConfiguration.GetSection("system.web/machineKey") is not MachineKeySection machineKey)
        {
            return;
        }

        var validation = MachineKeyEnvironmentOverrides.Apply(
            machineKey.ValidationKey,
            configuration.MachineKeyValidationKeyOverride,
            "validationKey",
            MachineKeyEnvironmentOverrides.ValidationKeyVariable);
        var decryption = MachineKeyEnvironmentOverrides.Apply(
            machineKey.DecryptionKey,
            configuration.MachineKeyDecryptionKeyOverride,
            "decryptionKey",
            MachineKeyEnvironmentOverrides.DecryptionKeyVariable);
        ValidateSuppliedKey(
            configuration.MachineKeyValidationKeyOverride,
            MachineKeyEnvironmentOverrides.ValidationKeyVariable,
            minimumHexLength: 40);
        ValidateSuppliedKey(
            configuration.MachineKeyDecryptionKeyOverride,
            MachineKeyEnvironmentOverrides.DecryptionKeyVariable,
            minimumHexLength: 2);

        var autoValidation = MachineKeyEnvironmentOverrides.IsAutoGenerated(validation);
        var autoDecryption = MachineKeyEnvironmentOverrides.IsAutoGenerated(decryption);
        if (!autoValidation && !autoDecryption)
        {
            return;
        }

        var keyFilePath = AutogenKeyStore.ResolveKeyFilePath(configuration, out _);
        AutogenKeyStore.LoadOrCreate(configuration);
        WebFormsRuntimeEventSource.Log.AutoGeneratedMachineKey(autoValidation, autoDecryption, keyFilePath);
    }

    private static void ValidateSuppliedKey(string supplied, string variableName, int minimumHexLength)
    {
        if (supplied == null)
        {
            return;
        }

        var key = supplied;
        if (key.EndsWith(",IsolateByAppId", StringComparison.Ordinal))
        {
            key = key[..^",IsolateByAppId".Length];
        }

        if (key.EndsWith(",IsolateApps", StringComparison.Ordinal))
        {
            key = key[..^",IsolateApps".Length];
        }

        if (key.Contains("AutoGenerate", StringComparison.Ordinal))
        {
            return;
        }

        var hex = key.Length >= minimumHexLength && (key.Length & 1) == 0;
        foreach (var character in key)
        {
            hex &= Uri.IsHexDigit(character);
        }

        if (!hex)
        {
            throw new ConfigurationErrorsException(
                $"""
                The {variableName} environment variable must hold an even-length hex key of at least {minimumHexLength} characters, optionally followed by ",IsolateApps" or ",IsolateByAppId".
                """);
        }
    }

    // MultiTargetingUtil.ValidateTargetFrameworkMoniker accepts both a bare version and a
    // full moniker, so both forms carry a version this refusal has to read.
    private static void ValidateCompilationTargetFramework(CompilationSection compilation)
    {
        var configured = compilation.TargetFramework?.Trim();
        if (String.IsNullOrEmpty(configured))
        {
            return;
        }

        var moniker = Char.IsDigit(configured[0]) && Version.TryParse(configured, out _)
            ? $".NETFramework,Version=v{configured}"
            : configured;

        Version version;
        try
        {
            version = new FrameworkName(moniker).Version;
        }
        catch (ArgumentException)
        {
            return;
        }

        if (version >= new Version(4, 0))
        {
            return;
        }

        throw new PlatformNotSupportedException(
            $"""
            <compilation targetFramework="{configured}"> is not supported. The rehosted runtime supports targetFramework="4.0" and later; remove the attribute or raise its value.
            """);
    }

    // Two owners of one directory is a configuration mistake, not a precedence question, so a
    // configured value that disagrees with the host-supplied one fails instead of losing silently.
    internal static void ValidateCompilationTempDirectory(
        ApplicationBootstrapConfiguration configuration,
        CompilationSection compilation)
    {
        var host = configuration.CompilationTempDirectory;
        var environment = configuration.CompilationTempDirectoryOverride;
        if (host != null && environment != null && !PathsEqual(host, environment))
        {
            throw new InvalidOperationException(
                $"The {WebFormsApplicationOptions.CompilationTempDirectoryVariable} environment " +
                $"variable conflicts with the {CodegenDirectory.HostSource}. " +
                $"Variable='{environment}', Host='{host}'. Remove one of them.");
        }

        var owner = host ?? environment;
        var ownerSource = host != null ? CodegenDirectory.HostSource : CodegenDirectory.EnvironmentSource;
        var configured = compilation?.TempDirectory;
        string normalized = null;
        if (!String.IsNullOrWhiteSpace(configured))
        {
            configured = configured.Trim();
            normalized = Path.IsPathFullyQualified(configured)
                ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured))
                : configured;
        }

        if (owner != null && normalized != null && !PathsEqual(normalized, owner))
        {
            throw new InvalidOperationException(
                $"The configured {CodegenDirectory.ConfiguredSource} conflicts with " +
                $"the {ownerSource}. Configured='{configured}', " +
                $"Supplied='{owner}'. Remove one of them.");
        }

        var supplied = owner ?? normalized;
        if (supplied != null && IsInside(supplied, configuration.PhysicalRootPath))
        {
            WebFormsRuntimeEventSource.Log.CompilationOutputInsideApplication(
                supplied,
                owner != null ? ownerSource : CodegenDirectory.ConfiguredSource);
        }
    }

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool IsInside(string path, string directoryWithTrailingSeparator) =>
        (Path.TrimEndingDirectorySeparator(path) + Path.DirectorySeparatorChar)
            .StartsWith(directoryWithTrailingSeparator, PathComparison);

    private static bool PathsEqual(string left, string right) =>
        String.Equals(left, right, PathComparison);

    // The two out-of-process stores construct eagerly in SessionStateModule's mode
    // switch, so without this the failure is a native PlatformNotSupportedException
    // on the first request instead of a named refusal at activation.
    private static void ValidateSessionStateMode(SessionStateSection sessionState)
    {
        switch (sessionState.Mode)
        {
            case SessionStateMode.StateServer:
                throw new PlatformNotSupportedException(
                    "Session state mode=\"StateServer\" is not supported. The rehosted " +
                    "runtime supports mode=\"InProc\", mode=\"Custom\", and mode=\"Off\".");

            case SessionStateMode.SQLServer:
                throw new PlatformNotSupportedException(
                    "Session state mode=\"SQLServer\" is not yet implemented. The rehosted " +
                    "runtime supports mode=\"InProc\", mode=\"Custom\", and mode=\"Off\".");
        }
    }

    // The section default is Windows, so the mode alone does not mean the application asked for it.
    private static void ValidateAuthenticationMode(AuthenticationSection authentication)
    {
        if (authentication.Mode != AuthenticationMode.Windows)
        {
            return;
        }

        var mode = authentication.ElementInformation.Properties["mode"];
        if (mode is null || mode.ValueOrigin == PropertyValueOrigin.Default)
        {
            return;
        }

        throw new PlatformNotSupportedException(
            "<authentication mode=\"Windows\"> is not supported. No host supplies a Windows login, " +
            "so every request would stay anonymous and authorization rules would not apply. Use " +
            "mode=\"Forms\" or mode=\"None\".");
    }

    private static TSection RequireSection<TSection>(
        Configuration configuration,
        string sectionName)
        where TSection : ConfigurationSection
    {
        var section = configuration.GetSection(sectionName) as TSection;
        if (section == null)
        {
            throw new ConfigurationErrorsException(
                $"Required configuration section '{sectionName}' is missing.");
        }

        return section;
    }

    private static void ValidateRequiredFile(string path, string sourceName)
    {
        ValidateFile(path, sourceName, required: true);
    }

    private static void ValidateOptionalFile(string path, string sourceName)
    {
        ValidateFile(path, sourceName, required: false);
    }

    private static void ValidateFile(string path, string sourceName, bool required)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException) when (!required)
        {
            return;
        }
        catch (DirectoryNotFoundException) when (!required)
        {
            return;
        }
        catch (FileNotFoundException exception)
        {
            throw new FileNotFoundException(
                $"The {sourceName} file does not exist: '{path}'.",
                path,
                exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new FileNotFoundException(
                $"The {sourceName} file does not exist: '{path}'.",
                path,
                exception);
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException(
                $"The {sourceName} path is a directory, not a file: '{path}'.");
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException ||
            exception is IOException)
        {
            throw new IOException(
                $"The {sourceName} file cannot be read: '{path}'.",
                exception);
        }
    }
}
