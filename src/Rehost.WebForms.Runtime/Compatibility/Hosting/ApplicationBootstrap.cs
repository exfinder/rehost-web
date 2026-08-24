namespace Rehost.WebForms.Hosting;

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Web;
using System.Web.Configuration;
using System.Web.SessionState;
using System.Web.Util;

internal enum ApplicationBootstrapState
{
    Uninitialized,
    Initializing,
    Initialized,
    Faulted,
}

internal sealed class ApplicationBootstrap
{
    private readonly IApplicationBootstrapEnvironment _environment;
    private int _state;
    private ApplicationBootstrapConfiguration _configuration;

    internal ApplicationBootstrap(IApplicationBootstrapEnvironment environment)
    {
        _environment = environment;
    }

    internal ApplicationBootstrapState State => (ApplicationBootstrapState)Volatile.Read(ref _state);

    internal ApplicationBootstrapConfiguration Configuration => Volatile.Read(ref _configuration);

    internal void Initialize(WebFormsApplicationOptions options)
    {
        var priorState = Interlocked.CompareExchange(
            ref _state,
            (int)ApplicationBootstrapState.Initializing,
            (int)ApplicationBootstrapState.Uninitialized);
        if (priorState != (int)ApplicationBootstrapState.Uninitialized)
        {
            throw new InvalidOperationException(
                "WebFormsApplication.Initialize must be called exactly once per process.");
        }

        try
        {
            var configuration = ApplicationBootstrapConfiguration.Create(options, _environment.BaseDirectory);
            var serverConfiguration = System.Web.IisConfig.IisServerConfiguration.Load(
                configuration.ServerConfigurationFilePath,
                configuration.ApplicationConfigurationFilePath,
                configuration.VirtualRootPath);
            _environment.Preflight(configuration);
            _environment.Bind(configuration);
            System.Web.Configuration.HttpConfigurationSystem.SetConfigurationFilePaths(
                configuration.MachineConfigurationFilePath,
                configuration.RootWebConfigurationFilePath);
            System.Web.IisConfig.IisServerConfiguration.Publish(serverConfiguration);
            Volatile.Write(ref _configuration, configuration);
            Volatile.Write(ref _state, (int)ApplicationBootstrapState.Initialized);
        }
        catch
        {
            Volatile.Write(ref _state, (int)ApplicationBootstrapState.Faulted);
            throw;
        }
    }
}

internal sealed class ApplicationBootstrapConfiguration
{
    internal const string DefaultMachineConfigurationFileName =
        WebFormsApplicationOptions.DefaultMachineConfigurationFileName;
    internal const string DefaultRootWebConfigurationFileName =
        WebFormsApplicationOptions.DefaultRootWebConfigurationFileName;

    private ApplicationBootstrapConfiguration(
        string applicationId,
        string physicalRootPath,
        string virtualRootPath,
        string machineConfigurationFilePath,
        string rootWebConfigurationFilePath,
        string compilationTempDirectory,
        string machineKeyDirectory,
        string machineKeyValidationKeyOverride,
        string machineKeyDecryptionKeyOverride)
    {
        ApplicationId = applicationId;
        PhysicalRootPath = physicalRootPath;
        VirtualRootPath = virtualRootPath;
        MachineConfigurationFilePath = machineConfigurationFilePath;
        RootWebConfigurationFilePath = rootWebConfigurationFilePath;
        CompilationTempDirectory = compilationTempDirectory;
        MachineKeyDirectory = machineKeyDirectory;
        MachineKeyValidationKeyOverride = machineKeyValidationKeyOverride;
        MachineKeyDecryptionKeyOverride = machineKeyDecryptionKeyOverride;
    }

    internal string ApplicationId { get; }

    internal string PhysicalRootPath { get; }

    internal string VirtualRootPath { get; }

    internal string MachineConfigurationFilePath { get; }

    internal string RootWebConfigurationFilePath { get; }

    // Null means no host-supplied root; resolution then falls to configured tempDirectory and to
    // the portable default. See CodegenDirectory.
    internal string CompilationTempDirectory { get; }

    // Null means no host-supplied directory; resolution then falls to the per-user default. See
    // AutogenKeyStore.
    internal string MachineKeyDirectory { get; }

    internal string MachineKeyValidationKeyOverride { get; }

    internal string MachineKeyDecryptionKeyOverride { get; }

    internal string ApplicationConfigurationFilePath =>
        Path.Combine(PhysicalRootPath, HttpConfigurationSystem.WebConfigFileName);

    // The IIS half of the baseline ships beside the Framework mirrors and travels with them
    // when MachineConfigurationFilePath is overridden.
    internal string ServerConfigurationFilePath =>
        Path.Combine(
            Path.GetDirectoryName(MachineConfigurationFilePath)!,
            "rehost-webforms.applicationHost.config");

    internal IConfigMapPathFactory ConfigMapPathFactory => new PortableConfigMapPathFactory(this);

    internal static ApplicationBootstrapConfiguration Create(
        WebFormsApplicationOptions options,
        string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);

        var applicationId = RequireNonWhiteSpace(options.ApplicationId, nameof(options.ApplicationId));
        var physicalRootPath = NormalizePhysicalRoot(options.PhysicalRootPath);
        var virtualRootPath = NormalizeVirtualRoot(options.VirtualRootPath);
        var configurationDirectory = Path.Combine(
            Path.GetFullPath(baseDirectory),
            "configs");
        var machineConfigurationFilePath = NormalizeConfigurationFilePath(
            options.MachineConfigurationFilePath,
            Path.Combine(configurationDirectory, DefaultMachineConfigurationFileName),
            nameof(options.MachineConfigurationFilePath));
        var rootWebConfigurationFilePath = NormalizeConfigurationFilePath(
            options.RootWebConfigurationFilePath,
            Path.Combine(configurationDirectory, DefaultRootWebConfigurationFileName),
            nameof(options.RootWebConfigurationFilePath));

        return new ApplicationBootstrapConfiguration(
            applicationId,
            physicalRootPath,
            virtualRootPath,
            machineConfigurationFilePath,
            rootWebConfigurationFilePath,
            NormalizeDirectoryOption(
                options.CompilationTempDirectory,
                "compilation temporary directory",
                nameof(WebFormsApplicationOptions.CompilationTempDirectory)),
            NormalizeDirectoryOption(
                options.MachineKeyDirectory,
                "machine-key directory",
                nameof(WebFormsApplicationOptions.MachineKeyDirectory)),
            ReadEnvironmentOverride(MachineKeyEnvironmentOverrides.ValidationKeyVariable),
            ReadEnvironmentOverride(MachineKeyEnvironmentOverrides.DecryptionKeyVariable));
    }

    private static string ReadEnvironmentOverride(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal WebConfigurationFileMap CreateFileMap()
    {
        var map = new WebConfigurationFileMap(MachineConfigurationFilePath);
        map.VirtualDirectories.Add(
            null,
            new VirtualDirectoryMapping(
                Path.GetDirectoryName(RootWebConfigurationFilePath),
                false,
                Path.GetFileName(RootWebConfigurationFilePath)));
        map.VirtualDirectories.Add(
            VirtualRootPath,
            new VirtualDirectoryMapping(
                Path.TrimEndingDirectorySeparator(PhysicalRootPath),
                true));
        return map;
    }

    private static string RequireNonWhiteSpace(string value, string parameterName)
    {
        if (String.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty application ID is required.", parameterName);
        }

        return value;
    }

    private static string NormalizePhysicalRoot(string path)
    {
        if (String.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "An absolute physical application root is required.",
                nameof(WebFormsApplicationOptions.PhysicalRootPath));
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                $"The physical application root must be absolute: '{path}'.",
                nameof(WebFormsApplicationOptions.PhysicalRootPath));
        }

        var fullPath = Path.GetFullPath(path);
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(fullPath);
        }
        catch (FileNotFoundException exception)
        {
            throw new DirectoryNotFoundException(
                $"The physical application root does not exist: '{fullPath}'.",
                exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new DirectoryNotFoundException(
                $"The physical application root does not exist: '{fullPath}'.",
                exception);
        }

        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new ArgumentException(
                $"The physical application root is not a directory: '{fullPath}'.",
                nameof(WebFormsApplicationOptions.PhysicalRootPath));
        }

        return Path.EndsInDirectorySeparator(fullPath)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private static string NormalizeVirtualRoot(string path)
    {
        if (String.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "An absolute virtual application root is required.",
                nameof(WebFormsApplicationOptions.VirtualRootPath));
        }

        if (path.IndexOf('\\') >= 0 || path.IndexOf('?') >= 0 || path.IndexOf('#') >= 0)
        {
            throw new ArgumentException(
                $"The virtual application root is invalid: '{path}'.",
                nameof(WebFormsApplicationOptions.VirtualRootPath));
        }

        foreach (var segment in path.Split('/'))
        {
            if (segment == "." || segment == "..")
            {
                throw new ArgumentException(
                    $"The virtual application root cannot contain traversal segments: '{path}'.",
                    nameof(WebFormsApplicationOptions.VirtualRootPath));
            }
        }

        try
        {
            return VirtualPath.CreateAbsolute(path).VirtualPathStringNoTrailingSlash;
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is HttpException)
        {
            throw new ArgumentException(
                $"The virtual application root is invalid: '{path}'.",
                nameof(WebFormsApplicationOptions.VirtualRootPath),
                exception);
        }
    }

    private static string NormalizeDirectoryOption(string path, string description, string parameterName)
    {
        if (String.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                $"The {description} must be absolute: '{path}'.",
                parameterName);
        }

        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(fullPath);
        }
        catch (FileNotFoundException)
        {
            return fullPath;
        }
        catch (DirectoryNotFoundException)
        {
            return fullPath;
        }

        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new ArgumentException(
                $"The {description} is a file, not a directory: '{fullPath}'.",
                parameterName);
        }

        return fullPath;
    }

    private static string NormalizeConfigurationFilePath(
        string configuredPath,
        string defaultPath,
        string parameterName)
    {
        var path = String.IsNullOrWhiteSpace(configuredPath)
            ? defaultPath
            : configuredPath;
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                $"The configuration file path must be absolute: '{path}'.",
                parameterName);
        }

        return Path.GetFullPath(path);
    }
}

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
            var mappedConfiguration = WebConfigurationManager.OpenMappedWebConfiguration(
                configuration.CreateFileMap(),
                configuration.VirtualRootPath,
                WebConfigurationHost.DefaultSiteID);
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

            ValidateCompilationTempDirectory(configuration, compilation);
            ValidateMachineKey(configuration, mappedConfiguration);
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

    // Reads the declared key strings only. Resolving them would generate the keys, which is what
    // RuntimeDataInitialize does on first use.
    internal static void ValidateMachineKey(
        ApplicationBootstrapConfiguration configuration,
        Configuration mappedConfiguration)
    {
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
        WebFormsRuntimeEventSource.Log.AutoGeneratedMachineKey(
            autoValidation && autoDecryption
                ? "validationKey and decryptionKey"
                : autoValidation ? "validationKey" : "decryptionKey",
            keyFilePath);
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

    // Two owners of one directory is a configuration mistake, not a precedence question, so a
    // configured value that disagrees with the host-supplied one fails instead of losing silently.
    internal static void ValidateCompilationTempDirectory(
        ApplicationBootstrapConfiguration configuration,
        CompilationSection compilation)
    {
        var configured = compilation?.TempDirectory;
        if (configuration.CompilationTempDirectory == null ||
            String.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        configured = configured.Trim();
        var normalized = Path.IsPathFullyQualified(configured)
            ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured))
            : configured;

        if (!String.Equals(
                normalized,
                configuration.CompilationTempDirectory,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The configured system.web/compilation tempDirectory conflicts with the host " +
                $"CompilationTempDirectory option. Configured='{configured}', " +
                $"Host='{configuration.CompilationTempDirectory}'. Remove one of them.");
        }
    }

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

internal interface IApplicationData
{
    object Get(string key);

    void Set(string key, object value);
}

internal sealed class AppDomainApplicationData : IApplicationData
{
    public object Get(string key)
    {
        return AppDomain.CurrentDomain.GetData(key);
    }

    public void Set(string key, object value)
    {
        AppDomain.CurrentDomain.SetData(key, value);
    }
}

internal static class ApplicationBinding
{
    private static readonly string[] Keys =
    {
        ".appId",
        ".appPath",
        ".appVPath",
        ".domainId",
        ".appDomain",
    };

    internal static void Bind(
        ApplicationBootstrapConfiguration configuration,
        IApplicationData applicationData)
    {
        var previousValues = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var key in Keys)
        {
            previousValues.Add(key, applicationData.Get(key));
        }

        foreach (var pair in previousValues)
        {
            if (pair.Value != null)
            {
                throw new InvalidOperationException(
                    $"The current AppDomain already contains a Web Forms application binding at '{pair.Key}'.");
            }
        }

        try
        {
            applicationData.Set(".appId", configuration.ApplicationId);
            applicationData.Set(".appPath", configuration.PhysicalRootPath);
            applicationData.Set(".appVPath", configuration.VirtualRootPath);
            applicationData.Set(".domainId", configuration.ApplicationId + "-current");
            applicationData.Set(".appDomain", "*");
        }
        catch
        {
            foreach (var key in Keys)
            {
                applicationData.Set(key, previousValues[key]);
            }

            throw;
        }
    }
}

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
