using Microsoft.Build.Framework;

namespace Rehost.Web.FixtureStaging.Tasks;

// Composes fixture bin directories from already-built payload files. Anything else in a warm
// bin is a leftover that would keep resolving, so non-payload files are deleted rather than
// left behind. Bins metadata scopes a payload file to the named fixture(s); empty means every
// bin. Subfolder metadata places it in that folder under the bin. The copy compares size and
// timestamp for inequality, like the tree staging.
public sealed class StageFixtureBins : Microsoft.Build.Utilities.Task
{
    [Required]
    public ITaskItem[] BinDirs { get; set; } = [];

    [Required]
    public ITaskItem[] Payload { get; set; } = [];

    public override bool Execute()
    {
        foreach (var file in Payload)
        {
            if (!File.Exists(file.ItemSpec))
            {
                Log.LogError("Fixture payload build output missing: {0}", file.ItemSpec);
                return false;
            }
        }

        foreach (var bin in BinDirs)
        {
            var binPath = bin.ItemSpec;
            var fixtureName = Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(binPath)))!;
            var files = Payload
                .Where(file =>
                {
                    var bins = file.GetMetadata("Bins");
                    return bins.Length == 0
                        || bins.Split(';').Contains(fixtureName, StringComparer.Ordinal);
                })
                .ToDictionary(
                    file => Path.Combine(file.GetMetadata("Subfolder"), Path.GetFileName(file.ItemSpec)),
                    file => file.ItemSpec,
                    StringComparer.Ordinal);

            Directory.CreateDirectory(binPath);
            foreach (var stale in Directory.GetFiles(binPath, "*", SearchOption.AllDirectories))
            {
                if (!files.ContainsKey(Path.GetRelativePath(binPath, stale)))
                {
                    File.Delete(stale);
                }
            }

            foreach (var (name, source) in files)
            {
                var target = Path.Combine(binPath, name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var sourceInfo = new FileInfo(source);
                var targetInfo = new FileInfo(target);
                if (targetInfo.Exists
                    && targetInfo.Length == sourceInfo.Length
                    && targetInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc)
                {
                    continue;
                }

                File.Copy(source, target, overwrite: true);
                File.SetLastWriteTimeUtc(target, sourceInfo.LastWriteTimeUtc);
            }
        }

        return true;
    }
}
