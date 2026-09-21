using System.IO.Compression;
using System.Xml.Linq;

namespace Rehost.WebForms.Packages.Tests;

public sealed class CandidatePackage
{
    private readonly string _path;

    private CandidatePackage(string path, string id, string version, IReadOnlyList<string> packageTypes, IReadOnlyList<string> files, IReadOnlyDictionary<string, string> dependencies, IReadOnlyList<string> symbolFiles)
    {
        _path = path;
        Id = id;
        Version = version;
        PackageTypes = packageTypes;
        Files = files;
        Dependencies = dependencies;
        SymbolFiles = symbolFiles;
    }

    public string Id { get; }

    public string Version { get; }

    public IReadOnlyList<string> PackageTypes { get; }

    public IReadOnlyList<string> Files { get; }

    public IReadOnlyDictionary<string, string> Dependencies { get; }

    public IReadOnlyList<string> SymbolFiles { get; }

    public IEnumerable<string> LibraryFiles => Files.Where(f => f.StartsWith("lib/", StringComparison.Ordinal));

    public string ReadText(string file)
    {
        using var package = ZipFile.OpenRead(_path);
        using var reader = new StreamReader(package.GetEntry(file)!.Open());
        return reader.ReadToEnd();
    }

    public static CandidatePackage Open(string nupkgPath)
    {
        using var package = ZipFile.OpenRead(nupkgPath);
        var nuspecEntry = package.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var nuspecStream = nuspecEntry.Open();
        var nuspec = XDocument.Load(nuspecStream);
        var metadata = nuspec.Root!.Elements().Single(e => e.Name.LocalName == "metadata");
        var id = metadata.Elements().Single(e => e.Name.LocalName == "id").Value;
        var version = metadata.Elements().Single(e => e.Name.LocalName == "version").Value;
        var packageTypes = nuspec.Descendants()
            .Where(e => e.Name.LocalName == "packageType")
            .Select(e => (string)e.Attribute("name")!)
            .ToList();
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

        return new CandidatePackage(nupkgPath, id, version, packageTypes, files, dependencies, symbolFiles);
    }
}
