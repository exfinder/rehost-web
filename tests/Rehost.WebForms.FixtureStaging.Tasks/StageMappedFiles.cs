using System.Xml;
using Microsoft.Build.Framework;

namespace Rehost.WebForms.FixtureStaging.Tasks;

// Stages individually mapped files, applying config edits to the bytes before they land: the
// staged file is compared against the *intended* content, not the source, so an edited file is
// neither perpetually stale (the timestamp gate this replaces) nor silently left mutated.
public sealed class StageMappedFiles : Microsoft.Build.Utilities.Task
{
    [Required]
    public ITaskItem[] Files { get; set; } = [];

    public ITaskItem[] ConfigEdits { get; set; } = [];

    public override bool Execute()
    {
        var staged = 0;
        foreach (var file in Files)
        {
            var source = file.GetMetadata("FullPath");
            var target = file.GetMetadata("StagedAs");
            if (target.Length == 0)
            {
                Log.LogError("Staged file {0} carries no StagedAs metadata.", source);
                return false;
            }

            if (!File.Exists(source))
            {
                Log.LogError("Staged file source does not exist: {0}", source);
                return false;
            }

            var intended = Intended(source, target);
            if (File.Exists(target) && intended.AsSpan().SequenceEqual(File.ReadAllBytes(target)))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, intended);
            staged++;
        }

        Log.LogMessage(MessageImportance.Low, "Staged {0} mapped file(s).", staged);
        return true;
    }

    private byte[] Intended(string source, string target)
    {
        var edits = ConfigEdits
            .Where(edit => string.Equals(
                Path.GetFullPath(edit.ItemSpec), Path.GetFullPath(target), StringComparison.Ordinal))
            .ToList();
        if (edits.Count == 0)
        {
            return File.ReadAllBytes(source);
        }

        var document = new XmlDocument { PreserveWhitespace = true };
        document.Load(source);
        foreach (var edit in edits)
        {
            var query = edit.GetMetadata("XPath");
            var node = document.SelectSingleNode(query)
                ?? throw new InvalidOperationException(
                    $"{source} has no node at {query}; the staged edit would silently no-op.");
            node.Value = edit.GetMetadata("Value");
        }

        using var buffer = new MemoryStream();
        document.Save(buffer);
        return buffer.ToArray();
    }
}
