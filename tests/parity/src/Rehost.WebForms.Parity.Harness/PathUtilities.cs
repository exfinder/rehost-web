using System;
using System.IO;

namespace Rehost.WebForms.Parity.Harness;

public static class PathUtilities
{
    public static string EnsureTrailingDirectorySeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}
