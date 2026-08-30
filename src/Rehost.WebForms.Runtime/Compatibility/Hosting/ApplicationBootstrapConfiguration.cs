namespace Rehost.WebForms.Hosting;

using System;
using System.IO;
using System.Web;
using System.Web.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        string compilationTempDirectoryOverride,
        string defaultCompilationTempDirectory,
        string machineKeyDirectory,
        string machineKeyValidationKeyOverride,
        string machineKeyDecryptionKeyOverride,
        ILoggerFactory loggerFactory)
    {
        ApplicationId = applicationId;
        PhysicalRootPath = physicalRootPath;
        VirtualRootPath = virtualRootPath;
        MachineConfigurationFilePath = machineConfigurationFilePath;
        RootWebConfigurationFilePath = rootWebConfigurationFilePath;
        CompilationTempDirectory = compilationTempDirectory;
        CompilationTempDirectoryOverride = compilationTempDirectoryOverride;
        DefaultCompilationTempDirectory = defaultCompilationTempDirectory;
        MachineKeyDirectory = machineKeyDirectory;
        MachineKeyValidationKeyOverride = machineKeyValidationKeyOverride;
        MachineKeyDecryptionKeyOverride = machineKeyDecryptionKeyOverride;
        LoggerFactory = loggerFactory;
    }

    internal string ApplicationId { get; }

    internal string PhysicalRootPath { get; }

    internal string VirtualRootPath { get; }

    internal string MachineConfigurationFilePath { get; }

    internal string RootWebConfigurationFilePath { get; }

    // Null means no host-supplied root; resolution then falls to the environment variable, the
    // configured tempDirectory, and the default. See CodegenDirectory.
    internal string CompilationTempDirectory { get; }

    internal string CompilationTempDirectoryOverride { get; }

    internal string DefaultCompilationTempDirectory { get; }

    // Null means no host-supplied directory; resolution then falls to the per-user default. See
    // AutogenKeyStore.
    internal string MachineKeyDirectory { get; }

    internal string MachineKeyValidationKeyOverride { get; }

    internal string MachineKeyDecryptionKeyOverride { get; }

    internal ILoggerFactory LoggerFactory { get; }

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
                ReadEnvironmentOverride(WebFormsApplicationOptions.CompilationTempDirectoryVariable),
                "compilation temporary directory from " +
                    WebFormsApplicationOptions.CompilationTempDirectoryVariable,
                WebFormsApplicationOptions.CompilationTempDirectoryVariable),
            Path.Combine(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory)),
                "codegen"),
            NormalizeDirectoryOption(
                options.MachineKeyDirectory,
                "machine-key directory",
                nameof(WebFormsApplicationOptions.MachineKeyDirectory)),
            ReadEnvironmentOverride(MachineKeyEnvironmentOverrides.ValidationKeyVariable),
            ReadEnvironmentOverride(MachineKeyEnvironmentOverrides.DecryptionKeyVariable),
            options.LoggerFactory ?? NullLoggerFactory.Instance);
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
