using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;

namespace SplitOutput.Tasks;

public sealed class RewriteSplitDependencies : Microsoft.Build.Utilities.Task
{
    [Required] public string DepsFile { get; set; } = "";
    [Required] public ITaskItem[] AssetLocations { get; set; } = [];
    [Required] public ITaskItem[] ApplicationLibraries { get; set; } = [];

    public override bool Execute()
    {
        try
        {
            var original = File.ReadAllText(DepsFile);
            var manifest = JsonNode.Parse(original)!.AsObject();
            var appLibraries = ApplicationLibraries.Select(item => item.ItemSpec).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var locations = AssetLocations.ToDictionary(
                item => $"{item.GetMetadata("LibraryName")}/{item.GetMetadata("AssetPath")}",
                item => item.ItemSpec, StringComparer.OrdinalIgnoreCase);
            foreach (var target in manifest["targets"]!.AsObject())
            {
                foreach (var library in target.Value!.AsObject())
                {
                    var libraryName = library.Key[..library.Key.LastIndexOf('/')];
                    if (!appLibraries.Contains(libraryName))
                    {
                        continue;
                    }
                    foreach (var sectionName in new[] { "runtime", "native", "resources", "runtimeTargets" })
                    {
                        if (library.Value![sectionName] is not JsonObject section)
                        {
                            continue;
                        }

                        foreach (var asset in section)
                        {
                            if (!locations.TryGetValue($"{libraryName}/{asset.Key}", out var localPath))
                            {
                                throw new InvalidOperationException($"No SDK copy-local asset matches '{libraryName}/{asset.Key}' in {DepsFile}.");
                            }
                            asset.Value!["localPath"] = localPath;
                        }
                    }
                }
            }

            var updated = manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            if (updated != original)
            {
                var temporary = $"{DepsFile}.{Guid.NewGuid():N}.tmp";
                try
                {
                    File.WriteAllText(temporary, updated);
                    File.Move(temporary, DepsFile, overwrite: true);
                }
                finally
                {
                    File.Delete(temporary);
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception);
            return false;
        }
    }
}
