namespace System.Web.Hosting;

using System;
using System.Configuration;
using System.IO;
using System.Web.Configuration;
using Rehost.WebForms.Hosting;

// Portable replacement for the two leaves Framework used to place generated output: the CLR
// install directory, which is not writable and holds no ASP.NET codegen root here, and the
// AppDomain dynamic directory, which is null outside .NET Framework. Both the root and the
// generation segment are derived from explicit application inputs so a restarted application
// finds the output of its previous run.
internal static class CodegenDirectory
{
    internal const string HostSource = "host CompilationTempDirectory option";

    internal const string EnvironmentSource =
        WebFormsApplicationOptions.CompilationTempDirectoryVariable + " environment variable";

    internal const string ConfiguredSource = "system.web/compilation tempDirectory";

    internal const string DefaultSource = "default per-user codegen directory";

    internal static string ResolveTempRoot(CompilationSection compilationSection, out string source)
    {
        string configured = null;
        string attributeName = null;
        string fileName = null;
        var lineNumber = 0;

        if (compilationSection != null && !String.IsNullOrEmpty(compilationSection.TempDirectory))
        {
            configured = compilationSection.TempDirectory;
            compilationSection.GetTempDirectoryErrorInfo(out attributeName, out fileName, out lineNumber);
        }

        var configuration = WebFormsApplication.RequireInitialized();
        var hostSupplied = configuration.CompilationTempDirectory
            ?? configuration.CompilationTempDirectoryOverride;
        source = configuration.CompilationTempDirectory != null
            ? HostSource
            : configuration.CompilationTempDirectoryOverride != null
                ? EnvironmentSource
                : configured != null ? ConfiguredSource : DefaultSource;
        var tempRoot = SelectTempRoot(
            hostSupplied,
            configured,
            attributeName,
            fileName,
            lineNumber,
            hostSupplied == null && configured == null
                ? DefaultTempRoot(UserProfileDirectory.Resolve())
                : null);

        EnsureWritable(tempRoot, source);

        return tempRoot;
    }

    // Never under the application: bin feeds the hash that decides whether output is reused.
    internal static string DefaultTempRoot(string userProfile)
    {
        if (String.IsNullOrEmpty(userProfile))
        {
            throw new InvalidOperationException(
                "Generated output persists under the user profile, but no user profile directory " +
                "is available. Set the host CompilationTempDirectory option or the " +
                $"{WebFormsApplicationOptions.CompilationTempDirectoryVariable} environment variable.");
        }

        return Path.Combine(userProfile, UserProfileDirectory.FolderName, "codegen");
    }

    internal static string SelectTempRoot(
        string hostSupplied,
        string configured,
        string attributeName,
        string fileName,
        int lineNumber,
        string defaultTempRoot)
    {
        if (hostSupplied != null)
        {
            return hostSupplied;
        }

        if (configured == null)
        {
            return defaultTempRoot;
        }

        var tempDirectory = configured.Trim();

        // A relative path is rejected rather than resolved, because no ambient working directory
        // participates in a supported configuration.
        if (Path.IsPathRooted(tempDirectory))
        {
            try
            {
                return Path.TrimEndingDirectorySeparator(new DirectoryInfo(tempDirectory).FullName);
            }
            catch
            {
            }
        }

        throw new ConfigurationErrorsException(
            SR.GetString(SR.Invalid_temp_directory, attributeName),
            fileName,
            lineNumber);
    }

    // The generation segment replaces the identity-derived subdirectories the CLR appended through
    // AppDomain.SetDynamicBase. It is keyed on the application directory rather than on the host
    // application ID, so renaming the host label keeps the previous run's compiled output.
    internal static string GenerationSegment(string physicalApplicationRoot) =>
        ApplicationPathDigest.Segment(physicalApplicationRoot, byteCount: 8);

    private static void EnsureWritable(string tempRoot, string source)
    {
        try
        {
            Directory.CreateDirectory(tempRoot);
        }
        catch (Exception exception)
        {
            throw new HttpException(
                $"The compilation temporary directory '{tempRoot}' cannot be created. It comes from the {source}.",
                exception);
        }

        if (!System.Web.UI.Util.HasWriteAccessToDirectory(tempRoot))
        {
            throw new HttpException(
                $"The compilation temporary directory '{tempRoot}' is not writable. It comes from the {source}.");
        }
    }
}
