namespace Rehost.Web.AspNetCore;

using System;
using System.IO;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

// PhysicalFileProvider answers Exists=false for a real folder, which leaves the rules'
// IsDirectory condition false and sends directory requests down a catch-all IIS never took.
internal sealed class DirectoryAwareFileProvider : IFileProvider
{
    private readonly PhysicalFileProvider _physical;
    private readonly string _root;

    internal DirectoryAwareFileProvider(string root)
    {
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _physical = new PhysicalFileProvider(_root);
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        var full = Path.GetFullPath(Path.Combine(
            _root, (subpath ?? "").TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

        return IsWithinRoot(full) && Directory.Exists(full)
            ? new DirectoryEntry(full)
            : _physical.GetFileInfo(subpath!);
    }

    public IDirectoryContents GetDirectoryContents(string subpath) =>
        _physical.GetDirectoryContents(subpath);

    public IChangeToken Watch(string filter) => _physical.Watch(filter);

    private bool IsWithinRoot(string full) =>
        full.Length == _root.Length
            ? string.Equals(full, _root, StringComparison.Ordinal)
            : full.StartsWith(_root, StringComparison.Ordinal)
                && full[_root.Length] == Path.DirectorySeparatorChar;

    private sealed class DirectoryEntry(string full) : IFileInfo
    {
        public bool Exists => true;

        public bool IsDirectory => true;

        public long Length => -1;

        public string PhysicalPath => full;

        public string Name => Path.GetFileName(full);

        public DateTimeOffset LastModified => Directory.GetLastWriteTimeUtc(full);

        public Stream CreateReadStream() =>
            throw new InvalidOperationException($"'{full}' is a directory.");
    }
}
