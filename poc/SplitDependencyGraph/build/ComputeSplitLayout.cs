using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text.Json;

namespace SplitOutput.Tasks;

public sealed class ComputeSplitLayout : Microsoft.Build.Utilities.Task
{
    [Required] public string AssetsFile { get; set; } = "";
    [Required] public string ProjectDirectory { get; set; } = "";
    [Required] public string ApplicationProject { get; set; } = "";
    [Required] public string TargetFramework { get; set; } = "";
    [Required] public string Subdirectory { get; set; } = "";
    [Required] public ITaskItem[] References { get; set; } = [];
    public string RuntimeIdentifier { get; set; } = "";
    [Output] public ITaskItem[] RelocatedReferences { get; set; } = [];
    [Output] public ITaskItem[] AssetLocations { get; set; } = [];
    [Output] public ITaskItem[] ApplicationLibraries { get; set; } = [];

    public override bool Execute()
    {
        try
        {
            Compute();
            return true;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception);
            return false;
        }
    }

    private void Compute()
    {
        var prefix = NormalizeRelativePath(Subdirectory).TrimEnd('/');
        using var assets = JsonDocument.Parse(File.ReadAllBytes(AssetsFile));
        var targetName = RuntimeIdentifier.Length == 0 ? TargetFramework : $"{TargetFramework}/{RuntimeIdentifier}";
        if (!assets.RootElement.GetProperty("targets").TryGetProperty(targetName, out var target))
        {
            throw new InvalidOperationException($"No restored target {targetName} in {AssetsFile}.");
        }

        var projects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var library in assets.RootElement.GetProperty("libraries").EnumerateObject())
        {
            if (library.Value.GetProperty("type").GetString() == "project")
            {
                var relative = library.Value.GetProperty("msbuildProject").GetString()!;
                projects[Path.GetFullPath(relative, ProjectDirectory)] = library.Name[..library.Name.LastIndexOf('/')];
            }
        }

        var appPath = Path.GetFullPath(ApplicationProject, ProjectDirectory);
        if (!projects.TryGetValue(appPath, out var appName))
        {
            throw new InvalidOperationException($"Application project '{appPath}' is absent from Host's restored graph.");
        }

        var libraries = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var library in target.EnumerateObject())
        {
            libraries[library.Name[..library.Name.LastIndexOf('/')]] = library.Value;
        }

        var closure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>([appName]);
        while (pending.TryPop(out var name))
        {
            if (!closure.Add(name) || !libraries.TryGetValue(name, out var library) ||
                !library.TryGetProperty("dependencies", out var dependencies))
            {
                continue;
            }

            foreach (var dependency in dependencies.EnumerateObject())
            {
                pending.Push(dependency.Name);
            }
        }

        var relocated = new List<ITaskItem>();
        var locations = new List<ITaskItem>();
        var destinations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in References)
        {
            var item = new TaskItem(reference);
            var projectPath = item.GetMetadata("MSBuildSourceProjectFile");
            var libraryName = item.GetMetadata("NuGetPackageId");
            if (projectPath.Length > 0)
            {
                projects.TryGetValue(Path.GetFullPath(projectPath, ProjectDirectory), out libraryName);
            }

            var originalPath = NormalizeRelativePath($"{item.GetMetadata("DestinationSubDirectory")}{Path.GetFileName(item.ItemSpec)}");
            var destination = originalPath;
            if (libraryName is not null && closure.Contains(libraryName))
            {
                destination = $"{prefix}/{originalPath}";
                item.SetMetadata("DestinationSubDirectory", destination[..(destination.LastIndexOf('/') + 1)]);
                item.SetMetadata("DestinationSubPath", destination);
                var assetPath = item.GetMetadata("PathInPackage");
                var location = new TaskItem(destination);
                location.SetMetadata("LibraryName", libraryName);
                location.SetMetadata("AssetPath", assetPath.Length == 0 ? originalPath : assetPath.Replace('\\', '/'));
                locations.Add(location);
            }

            if (destinations.TryGetValue(destination, out var other) && other != item.ItemSpec)
            {
                throw new InvalidOperationException($"Both '{other}' and '{item.ItemSpec}' target '{destination}'.");
            }

            destinations[destination] = item.ItemSpec;
            relocated.Add(item);
        }

        RelocatedReferences = relocated.ToArray();
        AssetLocations = locations.ToArray();
        ApplicationLibraries = closure.Order(StringComparer.OrdinalIgnoreCase).Select(name => new TaskItem(name)).ToArray();
        Log.LogMessage(MessageImportance.High, $"Split output: {locations.Count} App assets → {prefix}/ (Host's resolved versions).");
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (Path.IsPathRooted(normalized) || normalized.Contains(':') ||
            normalized.Split('/').Any(segment => segment is "." or "..") || string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"Expected an output-relative path without traversal; got '{path}'.");
        }

        return normalized;
    }
}
