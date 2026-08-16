#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Web.Util;

namespace System.Web.Configuration;

// The <add assembly="*"/> scan of bin. Framework listed bin\*.dll — a case-insensitive glob in
// NTFS order — and loaded each by simple name; a file that was no assembly at all failed with
// COR_E_ASSEMBLYEXPECTED, the one HRESULT the loader ignores. The .NET loader reports such a
// file with a different HRESULT, so the port skips it here instead: what remains is a managed
// image whose load failure Framework also surfaced (wrong architecture, mismatched identity, a
// missing dependency). Foo.DLL is matched on every filesystem, and the order is NTFS's.
internal static class BinDirectoryScan
{
    internal static FileInfo[] ManagedAssemblyFiles(DirectoryInfo bin)
    {
        var files = new List<FileInfo>();
        foreach (var file in bin.EnumerateFiles())
        {
            if (file.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                && PortableExecutableFile.HasManagedMetadata(file.FullName))
            {
                files.Add(file);
            }
        }

        var sorted = files.ToArray();
        Array.Sort(sorted, static (x, y) => DirectoryOrder.NameComparer.Compare(x.Name, y.Name));
        return sorted;
    }
}
