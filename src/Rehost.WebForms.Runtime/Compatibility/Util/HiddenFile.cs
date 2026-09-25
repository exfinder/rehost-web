#nullable enable

using System.IO;

namespace System.Web.Util;

// .NET off Windows reports every dot-prefixed name as Hidden. Framework only ever read the Windows
// attribute, under which a leading dot is an ordinary name.
internal static class HiddenFile
{
    internal static bool IsHidden(FileSystemInfo entry) =>
        (entry.Attributes & FileAttributes.Hidden) != 0
        && (OperatingSystem.IsWindows() || !entry.Name.StartsWith('.'));
}
