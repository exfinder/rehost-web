using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Rehost.Web.FixtureStaging.Tasks;

// Mirrors a source fixture tree into the staged root: copy on difference, prune what source no
// longer has. The copy compares size and timestamp for INEQUALITY, not source-newer-than-dest:
// a staged copy mutated in place can carry any timestamp, including a future one, and must
// still be restored. A protected subtree (e.g. */bin, payload-owned) is exempt from pruning
// only while the components its wildcards bind to still exist in source; a deleted fixture's
// bin dies with the fixture.
public sealed class StageFixtureTree : Microsoft.Build.Utilities.Task
{
    [Required]
    public string SourceRoot { get; set; } = "";

    [Required]
    public string TargetRoot { get; set; } = "";

    public string[] ProtectedSubtrees { get; set; } = [];

    public override bool Execute()
    {
        if (!Directory.Exists(SourceRoot))
        {
            Log.LogError("Fixture source root does not exist: {0}", SourceRoot);
            return false;
        }

        var sourceFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(SourceRoot, "*", SearchOption.AllDirectories))
        {
            sourceFiles[Relative(SourceRoot, file)] = file;
        }

        var sourceDirectories = new HashSet<string>(
            Directory.GetDirectories(SourceRoot, "*", SearchOption.AllDirectories)
                .Select(directory => Relative(SourceRoot, directory)),
            StringComparer.Ordinal);

        var copied = 0;
        foreach (var (relative, source) in sourceFiles)
        {
            var target = Path.Combine(TargetRoot, relative);
            var sourceInfo = new FileInfo(source);
            var targetInfo = new FileInfo(target);
            if (targetInfo.Exists
                && targetInfo.Length == sourceInfo.Length
                && targetInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc)
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
            File.SetLastWriteTimeUtc(target, sourceInfo.LastWriteTimeUtc);
            copied++;
        }

        var prunedFiles = 0;
        var prunedDirectories = 0;
        if (Directory.Exists(TargetRoot))
        {
            foreach (var file in Directory.GetFiles(TargetRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Relative(TargetRoot, file);
                if (sourceFiles.ContainsKey(relative) || IsProtected(relative, sourceDirectories))
                {
                    continue;
                }

                File.Delete(file);
                prunedFiles++;
            }

            foreach (var directory in Directory
                .GetDirectories(TargetRoot, "*", SearchOption.AllDirectories)
                .OrderByDescending(directory => directory.Length))
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                var relative = Relative(TargetRoot, directory);
                if (sourceDirectories.Contains(relative)
                    || IsProtected(relative, sourceDirectories))
                {
                    continue;
                }

                Directory.Delete(directory, recursive: true);
                prunedDirectories++;
            }
        }

        Log.LogMessage(
            MessageImportance.Low,
            "Staged fixture tree {0}: {1} copied, {2} files and {3} directories pruned.",
            TargetRoot,
            copied,
            prunedFiles,
            prunedDirectories);
        return true;
    }

    private bool IsProtected(string relative, HashSet<string> sourceDirectories)
    {
        var components = relative.Split('/');
        foreach (var pattern in ProtectedSubtrees)
        {
            var patternComponents = pattern.Split('/');
            if (components.Length < patternComponents.Length)
            {
                continue;
            }

            var matches = true;
            for (var i = 0; i < patternComponents.Length; i++)
            {
                if (patternComponents[i] != "*" && patternComponents[i] != components[i])
                {
                    matches = false;
                    break;
                }
            }

            if (matches
                && sourceDirectories.Contains(
                    string.Join('/', components.Take(patternComponents.Length - 1))))
            {
                return true;
            }
        }

        return false;
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
