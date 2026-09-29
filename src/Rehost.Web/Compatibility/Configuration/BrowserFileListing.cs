#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Web.Util;

namespace System.Web.Configuration;

internal static class BrowserFileListing
{
    private static readonly EnumerationOptions BrowserFilePattern = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        MatchType = MatchType.Win32,
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
    };

    internal static DirectoryInfo[] Subdirectories(DirectoryInfo directory) =>
        DirectoryOrder.Sort(directory.GetDirectories());

    // Framework's recursive listing took each directory's children as one block and descended into
    // the first of them before the next sibling; .NET's SearchOption.AllDirectories goes breadth-first.
    internal static DirectoryInfo[] AllSubdirectories(DirectoryInfo root)
    {
        var listed = new List<DirectoryInfo>();
        var pending = new List<DirectoryInfo> { root };
        while (pending.Count > 0)
        {
            var children = Subdirectories(pending[0]);
            pending.RemoveAt(0);
            listed.AddRange(children);
            pending.InsertRange(0, children);
        }

        return listed.ToArray();
    }

    internal static FileInfo[] BrowserFiles(DirectoryInfo directory) =>
        DirectoryOrder.Sort(directory.EnumerateFiles("*.browser", BrowserFilePattern));
}
