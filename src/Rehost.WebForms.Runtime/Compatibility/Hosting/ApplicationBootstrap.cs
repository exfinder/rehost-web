namespace Rehost.WebForms.Hosting;

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Web;
using System.Web.Configuration;

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
            _environment.Preflight(configuration);
            _environment.Bind(configuration);
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
    internal const string DefaultMachineConfigurationFileName = "rehost-webforms.machine.config";
    internal const string DefaultRootWebConfigurationFileName = "rehost-webforms.web.config";

    private ApplicationBootstrapConfiguration(
        string applicationId,
        string physicalRootPath,
        string virtualRootPath,
        string machineConfigurationFilePath,
        string rootWebConfigurationFilePath)
    {
        ApplicationId = applicationId;
        PhysicalRootPath = physicalRootPath;
        VirtualRootPath = virtualRootPath;
        MachineConfigurationFilePath = machineConfigurationFilePath;
        RootWebConfigurationFilePath = rootWebConfigurationFilePath;
    }

    internal string ApplicationId { get; }

    internal string PhysicalRootPath { get; }

    internal string VirtualRootPath { get; }

    internal string MachineConfigurationFilePath { get; }

    internal string RootWebConfigurationFilePath { get; }

    internal string ApplicationConfigurationFilePath =>
        Path.Combine(PhysicalRootPath, HttpConfigurationSystem.WebConfigFileName);

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
            rootWebConfigurationFilePath);
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
            RequireSection<CompilationSection>(
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
