using System.Runtime.InteropServices;
using System.Web.Configuration;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Configuration;

public sealed class BinDirectoryScanTests
{
    // Framework ignored a native library, an empty file, and a text file named .dll in bin (each
    // failed its load with the one HRESULT the wildcard scan forgives), matched Foo.DLL through a
    // case-insensitive glob, and listed the rest in NTFS order.
    [Fact]
    public void Lists_Managed_Assemblies_Only_Any_Extension_Case_In_Ntfs_Order()
    {
        var bin = Directory.CreateTempSubdirectory("rehost-bin-scan-");
        try
        {
            var managed = typeof(BinDirectoryScanTests).Assembly.Location;
            foreach (var name in new[] { "zeta.dll", "Managed.DLL", "_last.dll", "9first.dll" })
            {
                File.Copy(managed, Path.Combine(bin.FullName, name));
            }

            File.Copy(managed, Path.Combine(bin.FullName, "NotScanned.exe"));
            File.Copy(RuntimeNativeLibrary(), Path.Combine(bin.FullName, "Native.dll"));
            File.WriteAllBytes(Path.Combine(bin.FullName, "Zero.dll"), []);
            File.WriteAllText(Path.Combine(bin.FullName, "Text.dll"), "hello");

            var names = BinDirectoryScan.ManagedAssemblyFiles(bin).Select(file => file.Name).ToList();

            names.ShouldBe(["9first.dll", "Managed.DLL", "zeta.dll", "_last.dll"]);
        }
        finally
        {
            bin.Delete(recursive: true);
        }
    }

    // A native image: a real PE without metadata on Windows, an ELF/Mach-O elsewhere.
    private static string RuntimeNativeLibrary()
    {
        var name = OperatingSystem.IsWindows() ? "coreclr.dll"
            : OperatingSystem.IsMacOS() ? "libcoreclr.dylib"
            : "libcoreclr.so";
        return Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), name);
    }
}
