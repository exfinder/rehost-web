using System.IO.Compression;
using System.Xml.Linq;

namespace Rehost.WebForms.Packages.Tests;

public sealed class CandidatePackage
{
    private CandidatePackage(string id, string version, IReadOnlyList<string> files, IReadOnlyDictionary<string, string> dependencies, IReadOnlyList<string> symbolFiles)
    {
        Id = id;
        Version = version;
        Files = files;
        Dependencies = dependencies;
        SymbolFiles = symbolFiles;
    }

    public string Id { get; }

    public string Version { get; }

    public IReadOnlyList<string> Files { get; }

    public IReadOnlyDictionary<string, string> Dependencies { get; }

    public IReadOnlyList<string> SymbolFiles { get; }

    public IEnumerable<string> LibraryFiles => Files.Where(f => f.StartsWith("lib/", StringComparison.Ordinal));

    public static CandidatePackage Open(string nupkgPath)
    {
        using var package = ZipFile.OpenRead(nupkgPath);
        var nuspecEntry = package.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var nuspecStream = nuspecEntry.Open();
        var nuspec = XDocument.Load(nuspecStream);
        var metadata = nuspec.Root!.Elements().Single(e => e.Name.LocalName == "metadata");
        var id = metadata.Elements().Single(e => e.Name.LocalName == "id").Value;
        var version = metadata.Elements().Single(e => e.Name.LocalName == "version").Value;
        var dependencies = nuspec.Descendants()
            .Where(e => e.Name.LocalName == "dependency")
            .ToDictionary(e => (string)e.Attribute("id")!, e => (string)e.Attribute("version")!, StringComparer.OrdinalIgnoreCase);
        var files = package.Entries.Select(e => e.FullName).ToList();

        var snupkgPath = Path.ChangeExtension(nupkgPath, ".snupkg");
        var symbolFiles = new List<string>();
        if (File.Exists(snupkgPath))
        {
            using var symbols = ZipFile.OpenRead(snupkgPath);
            symbolFiles.AddRange(symbols.Entries.Select(e => e.FullName));
        }

        return new CandidatePackage(id, version, files, dependencies, symbolFiles);
    }
}
