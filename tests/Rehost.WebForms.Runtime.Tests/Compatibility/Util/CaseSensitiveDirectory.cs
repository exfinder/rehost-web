using System.Diagnostics;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Util;

// A directory on a genuinely case-sensitive filesystem, or null where the platform cannot
// provide one. Linux temp already is one; macOS mounts a disposable case-sensitive APFS sparse
// image (no admin rights involved); Windows/NTFS cannot express the situation and callers skip.
// A twin copy exists in Rehost.WebForms.Hosting.Tests for the scenario-level tests.
internal sealed class CaseSensitiveDirectory : IDisposable
{
    private readonly DirectoryInfo? _plainRoot;
    private readonly string? _mountPoint;
    private readonly string? _imagePath;

    internal string? Path { get; }

    private CaseSensitiveDirectory(DirectoryInfo? plainRoot, string? mountPoint, string? imagePath)
    {
        _plainRoot = plainRoot;
        _mountPoint = mountPoint;
        _imagePath = imagePath;
        Path = mountPoint ?? plainRoot?.FullName;
    }

    internal static CaseSensitiveDirectory Create()
    {
        var probe = Directory.CreateTempSubdirectory("rehost-cs-probe-");
        try
        {
            File.WriteAllText(System.IO.Path.Combine(probe.FullName, "probe"), "");
            File.WriteAllText(System.IO.Path.Combine(probe.FullName, "PROBE"), "");
            if (probe.GetFiles().Length == 2)
            {
                foreach (var file in probe.GetFiles())
                {
                    file.Delete();
                }

                return new CaseSensitiveDirectory(probe, null, null);
            }
        }
        catch (IOException)
        {
        }

        probe.Delete(recursive: true);

        if (!OperatingSystem.IsMacOS())
        {
            return new CaseSensitiveDirectory(null, null, null);
        }

        var image = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "rehost-cs-" + Guid.NewGuid().ToString("N"));
        var mount = image + "-mnt";
        Run("hdiutil",
            "create", "-size", "64m", "-fs", "Case-sensitive APFS", "-type", "SPARSE",
            "-volname", "rehost-cs", image);
        Run("hdiutil", "attach", image + ".sparseimage", "-nobrowse", "-mountpoint", mount);
        return new CaseSensitiveDirectory(null, mount, image + ".sparseimage");
    }

    public void Dispose()
    {
        if (_mountPoint != null)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    Run("hdiutil", "detach", _mountPoint);
                    break;
                }
                catch (InvalidOperationException) when (attempt < 5)
                {
                    Thread.Sleep(200);
                }
            }

            File.Delete(_imagePath!);
        }

        _plainRoot?.Delete(recursive: true);
    }

    private static void Run(string command, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                command + " " + string.Join(" ", arguments) + " failed: "
                + process.StandardError.ReadToEnd());
        }
    }
}
