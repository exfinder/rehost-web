#nullable enable

using System.IO;

namespace System.Web.Util;

// Framework inherited end-to-end case-insensitivity from NTFS: /default.aspx found Default.aspx
// because the filesystem folded the difference. A case-sensitive filesystem misses instead, so
// when a mapped path does not exist this resolves each segment below the nearest existing
// directory — the application root for anything mapped under it, otherwise the deepest
// ancestor that exists — against the directory's real entries, ignoring case, and returns the
// file's true casing — one canonical spelling for every downstream consumer. An exact-case path
// never enters the fallback; a fallback segment matching more than one entry is an ambiguity
// Windows could never host and fails naming both; a genuine miss, or a directory that cannot
// be read, returns the input unchanged, preserving the 404 shape.
internal static class CanonicalCasePath
{
    internal static string Resolve(string mappedPath, string? appPhysicalRoot)
    {
        if (string.IsNullOrEmpty(mappedPath))
        {
            return mappedPath;
        }

        if (File.Exists(mappedPath) || Directory.Exists(mappedPath))
        {
            return mappedPath;
        }

        var hadTrailingSeparator =
            mappedPath.Length > 1 && mappedPath[^1] == Path.DirectorySeparatorChar;
        var path = hadTrailingSeparator ? mappedPath[..^1] : mappedPath;

        var start = StartDirectory(path, appPhysicalRoot);
        if (start == null)
        {
            return mappedPath;
        }

        var current = start;
        try
        {
            foreach (var segment in path[start.Length..].Split(Path.DirectorySeparatorChar))
            {
                if (segment.Length == 0)
                {
                    continue;
                }

                var exact = Path.Combine(current, segment);
                if (File.Exists(exact) || Directory.Exists(exact))
                {
                    current = exact;
                    continue;
                }

                if (!Directory.Exists(current))
                {
                    return mappedPath;
                }

                string? match = null;
                foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                {
                    var name = Path.GetFileName(entry);
                    if (!string.Equals(name, segment, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (match != null)
                    {
                        throw new HttpException(
                            500,
                            "The path '" + mappedPath + "' is ambiguous on this case-sensitive file"
                            + " system: '" + segment + "' matches both '" + Path.GetFileName(match)
                            + "' and '" + name + "' in '" + current + "'. Rename one so a single"
                            + " casing remains.");
                    }

                    match = entry;
                }

                if (match == null)
                {
                    return mappedPath;
                }

                current = match;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return mappedPath;
        }
        catch (IOException)
        {
            return mappedPath;
        }

        return hadTrailingSeparator ? current + Path.DirectorySeparatorChar : current;
    }

    private static string? StartDirectory(string path, string? appPhysicalRoot)
    {
        if (!string.IsNullOrEmpty(appPhysicalRoot))
        {
            var root = TrimTrailingSeparator(appPhysicalRoot);
            if (path.Length > root.Length + 1
                && path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                && path[root.Length] == Path.DirectorySeparatorChar)
            {
                return root;
            }
        }

        if (!Path.IsPathFullyQualified(path))
        {
            return null;
        }

        var ancestor = Path.GetDirectoryName(path);
        while (ancestor != null && !Directory.Exists(ancestor))
        {
            ancestor = Path.GetDirectoryName(ancestor);
        }

        return ancestor == null ? null : TrimTrailingSeparator(ancestor);
    }

    private static string TrimTrailingSeparator(string path) =>
        path[^1] == Path.DirectorySeparatorChar && Path.GetPathRoot(path) != path
            ? path[..^1]
            : path;
}
