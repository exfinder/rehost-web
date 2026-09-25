#nullable enable

using System.Collections.Generic;
using System.IO;

namespace System.Web.Util;

// NTFS returned directory entries in its index order — names compared code unit by code unit
// after upper-casing — and Framework applications inherited that as "alphabetical": which of two
// duplicate App_Code types the compiler names, the <link> order of a theme's style sheets, the
// reference order of a wildcard bin, the top-level hash that decides whether the codegen directory
// survives a restart. ext4 and APFS return hash order, so every port enumeration sorts this way.
// Ordinal breaks the tie a case-sensitive filesystem can hold and NTFS cannot (A.cs beside a.cs);
// neither comparison consults a culture.
internal static class DirectoryOrder
{
    internal static readonly IComparer<string> NameComparer = Comparer<string>.Create(CompareNames);

    internal static T[] Sort<T>(IEnumerable<T> entries)
        where T : FileSystemInfo
    {
        var sorted = new List<T>(entries).ToArray();
        Array.Sort(sorted, static (x, y) => CompareNames(x.Name, y.Name));
        return sorted;
    }

    internal static string[] SortPaths(string[] paths)
    {
        Array.Sort(paths, static (x, y) => CompareNames(Path.GetFileName(x), Path.GetFileName(y)));
        return paths;
    }

    private static int CompareNames(string x, string y)
    {
        var folded = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        return folded != 0 ? folded : string.CompareOrdinal(x, y);
    }
}
