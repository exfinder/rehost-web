namespace System.Web.Hosting;

using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Configuration;
using Rehost.WebForms.Hosting;

// Portable replacement for the two leaves Framework used to place generated output: the CLR
// install directory, which is not writable and holds no ASP.NET codegen root here, and the
// AppDomain dynamic directory, which is null outside .NET Framework. Both the root and the
// generation segment are derived from explicit application inputs so a restarted application
// finds the output of its previous run.
internal static class CodegenDirectory
{
    internal const string DefaultDirectoryName = "rehost-webforms-tempfiles";

    internal const string HostSource = "the host CompilationTempDirectory option";

    internal const string ConfiguredSource = "system.web/compilation tempDirectory";

    internal const string DefaultSource = "the default temporary directory";

    internal static string DefaultTempRoot =>
        Path.Combine(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), DefaultDirectoryName);

    internal static string ResolveTempRoot(CompilationSection compilationSection)
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

        var hostSupplied = WebFormsApplication.RequireInitialized().CompilationTempDirectory;
        var source = hostSupplied != null
            ? HostSource
            : configured != null ? ConfiguredSource : DefaultSource;
        var tempRoot = SelectTempRoot(hostSupplied, configured, attributeName, fileName, lineNumber);

        EnsureWritable(tempRoot, source);

        return tempRoot;
    }

    internal static string SelectTempRoot(
        string hostSupplied,
        string configured,
        string attributeName,
        string fileName,
        int lineNumber)
    {
        if (hostSupplied != null)
        {
            return hostSupplied;
        }

        if (configured == null)
        {
            return DefaultTempRoot;
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
    internal static string GenerationSegment(string physicalApplicationRoot)
    {
        var normalized = Path.TrimEndingDirectorySeparator(physicalApplicationRoot ?? String.Empty);
        if (OperatingSystem.IsWindows())
        {
            normalized = normalized.ToLowerInvariant();
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var segment = new StringBuilder(8);
        for (var i = 0; i < 4; i++)
        {
            segment.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return segment.ToString();
    }

    private static void EnsureWritable(string tempRoot, string source)
    {
        try
        {
            Directory.CreateDirectory(tempRoot);
        }
        catch (Exception exception)
        {
            throw new HttpException(
                $"The compilation temporary directory '{tempRoot}' cannot be created. It comes from {source}.",
                exception);
        }

        if (!System.Web.UI.Util.HasWriteAccessToDirectory(tempRoot))
        {
            throw new HttpException(
                $"The compilation temporary directory '{tempRoot}' is not writable. It comes from {source}.");
        }
    }
}
