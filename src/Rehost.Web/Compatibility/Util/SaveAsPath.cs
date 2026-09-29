#nullable enable

namespace System.Web.Util;

// Framework's SaveAs guard asks Path.IsPathRooted, whose answer is the running platform's. A path
// written for Windows is not rooted anywhere else, and off Windows it cannot name a file at all:
// with requireRootedSaveAsPath on it is refused as merely "not rooted", and with it off the whole
// path becomes the name of one file in the working directory, which looks like a successful save.
// Framework never ran off Windows, so nothing here diverges from it; the guard is inert wherever
// the platform agrees with Windows about the root.
internal static class SaveAsPath
{
    internal static void RequireUsableRoot(string? filename)
    {
        if (!IsRootedOnWindows(filename) || IO.Path.IsPathRooted(filename))
        {
            return;
        }

        throw new HttpException(
            "The SaveAs path '" + filename + "' is rooted only on Windows, and this process is "
            + "not running on Windows. Supply a path rooted on this platform, or build one with "
            + "Server.MapPath.");
    }

    private static bool IsRootedOnWindows(string? filename) =>
        !string.IsNullOrEmpty(filename)
        && (filename[0] is '\\' or '/'
            || (filename.Length > 1 && filename[1] == ':' && char.IsAsciiLetter(filename[0])));
}
