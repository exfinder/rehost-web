#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Enumeration;
using System.Web.Configuration;
using System.Web.Util;
using System.Xml;

namespace System.Web.IisConfig;

// <handlers> is resolved per directory, the way the managed configuration system keeps one record
// per path: every folder web.config below the application root is read, merged and turned into a
// handler list once at activation, and a request then only looks its directory up. The merge
// itself stays the measured collection algorithm - the folder's section is the application root's
// with one level added (MH27) - and the records are immutable, so a generation publishes complete
// lists or none.
//
// Directory keys fold case, which is the configuration-path rule (a configuration path is a
// lowercased virtual path); two folders differing only by case are therefore one path, and a
// case-sensitive filesystem that can hold both is refused rather than resolved arbitrarily.
internal sealed class IisFolderHandlers
{
    private readonly IReadOnlyList<IisHandlerRoute> _root;
    private readonly Dictionary<string, IReadOnlyList<IisHandlerRoute>> _folders;
    private readonly Dictionary<string, IReadOnlyList<IisHandlerRoute>>
        .AlternateLookup<ReadOnlySpan<char>> _lookup;
    private readonly string _virtualPrefix;

    private IisFolderHandlers(
        IReadOnlyList<IisHandlerRoute> root,
        Dictionary<string, IReadOnlyList<IisHandlerRoute>> folders,
        string virtualPrefix)
    {
        _root = root;
        _folders = folders;
        _lookup = folders.GetAlternateLookup<ReadOnlySpan<char>>();
        _virtualPrefix = virtualPrefix;
    }

    internal static IisFolderHandlers Empty { get; } = new(
        Array.Empty<IisHandlerRoute>(),
        new Dictionary<string, IReadOnlyList<IisHandlerRoute>>(StringComparer.OrdinalIgnoreCase),
        string.Empty);

    internal static IisFolderHandlers Load(
        IisRegistrationSection applicationSection,
        IReadOnlyList<IisHandlerRoute> applicationRoutes,
        string applicationPhysicalRoot,
        string applicationVirtualPath,
        bool applicationWaiver,
        Dictionary<string, string> hiddenSegments)
    {
        var prefix = VirtualPrefix(applicationVirtualPath);
        var folders = new Dictionary<string, IReadOnlyList<IisHandlerRoute>>(
            StringComparer.OrdinalIgnoreCase);
        var records = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in Discover(applicationPhysicalRoot, prefix, hiddenSegments))
        {
            var parent = NearestAncestor(records, folder.Key, prefix);
            var section = (parent?.Section ?? applicationSection).Nested();
            var waiver = Apply(folder.ConfigPath, section, parent?.Waiver ?? applicationWaiver);
            var routes = IisHandlerRoute.Build(section.Build());

            records.Add(folder.Key, new Record(section, waiver));
            folders.Add(folder.Key, routes);
        }

        return new IisFolderHandlers(applicationRoutes, folders, prefix);
    }

    // The deepest folder record covering the request's directory, or the application root's list
    // when no folder above it carries a <handlers> section - which is every request in an
    // application that has no folder web.config at all.
    internal IReadOnlyList<IisHandlerRoute> RoutesFor(VirtualPath? path)
    {
        if (_folders.Count == 0)
        {
            return _root;
        }

        var virtualPath = path?.VirtualPathStringIfAvailable;
        if (virtualPath == null || virtualPath.Length == 0 || virtualPath[0] != '/')
        {
            return _root;
        }

        var lastSlash = virtualPath.LastIndexOf('/');
        var directory = virtualPath.AsSpan(0, lastSlash);
        if (!directory.StartsWith(_virtualPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return _root;
        }

        while (directory.Length > _virtualPrefix.Length)
        {
            if (_lookup.TryGetValue(directory, out var routes))
            {
                return routes;
            }

            var slash = directory.LastIndexOf('/');
            if (slash < 0)
            {
                break;
            }

            directory = directory[..slash];
        }

        return _root;
    }

    private static string VirtualPrefix(string applicationVirtualPath)
    {
        var trimmed = applicationVirtualPath.TrimEnd('/');
        return trimmed == "/" ? string.Empty : trimmed;
    }

    private static Record? NearestAncestor(
        Dictionary<string, Record> records, string key, string prefix)
    {
        var directory = key;
        while (directory.Length > prefix.Length)
        {
            var slash = directory.LastIndexOf('/');
            if (slash < 0)
            {
                break;
            }

            directory = directory[..slash];
            if (records.TryGetValue(directory, out var record))
            {
                return record;
            }
        }

        return null;
    }

    // A folder's <modules> section is silently ignored, which is what IIS does with it (MH24);
    // reading one here would run modules IIS never ran. Nothing else in the folder file is honored
    // either, so only <handlers> and the classic-section rule are consulted.
    private static bool Apply(string configPath, IisRegistrationSection section, bool waiver)
    {
        var document = new XmlDocument();
        try
        {
            document.Load(configPath);
        }
        catch (XmlException failure)
        {
            // XmlException carries line and position but never the path, and activation reads
            // every folder file, so without this the operator cannot tell which one is broken.
            throw new ConfigurationErrorsException(
                $"'{configPath}' is not well-formed XML: {failure.Message}", failure);
        }

        var folderWaiver = ClassicSectionValidation.Validate(document, configPath, waiver);
        RewriteSection.RefuseBelowTheRoot(document, configPath);
        CustomHeaderSection.RefuseBelowTheRoot(document, configPath);
        HttpErrorsSection.RefuseBelowTheRoot(document, configPath);
        NativeAuthorization.Refuse(document, configPath);

        var handlersNode = document.SelectSingleNode(
            "/configuration/system.webServer/handlers");
        if (handlersNode != null)
        {
            section.Apply(handlersNode, configPath);
        }

        return folderWaiver;
    }

    // Web.config matches ignoring case on every filesystem; the chain is ordered here, not by enumeration.
    private static List<FolderConfig> Discover(
        string physicalRoot, string prefix, Dictionary<string, string> hiddenSegments)
    {
        var found = new List<FolderConfig>();
        if (!Directory.Exists(physicalRoot))
        {
            return found;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.None,
            IgnoreInaccessible = false,
        };

        var files = new FileSystemEnumerable<string>(
            physicalRoot,
            static (ref FileSystemEntry entry) => entry.ToFullPath(),
            options)
        {
            ShouldIncludePredicate = static (ref FileSystemEntry entry) =>
                !entry.IsDirectory
                && entry.FileName.Equals(
                    HttpConfigurationSystem.WebConfigFileName, StringComparison.OrdinalIgnoreCase),

            // A request whose path touches a hidden segment is refused, so a folder file below
            // one could never be selected and descending only costs activation time.
            ShouldRecursePredicate = (ref FileSystemEntry entry) =>
                !hiddenSegments.ContainsKey(entry.FileName.ToString()),
        };

        try
        {
            foreach (var configPath in files)
            {
                var relative = Path.GetRelativePath(
                    physicalRoot, Path.GetDirectoryName(configPath)!);
                if (relative == ".")
                {
                    continue;
                }

                var segments = relative.Split(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                found.Add(new FolderConfig(
                    prefix + "/" + string.Join('/', segments), segments.Length, configPath));
            }
        }
        catch (Exception failure) when (failure is UnauthorizedAccessException or IOException)
        {
            // Skipping the directory instead would answer its requests from the nearest ancestor
            // that was readable, which is a looser handler policy than the folder asked for.
            throw new ConfigurationErrorsException(
                $"A directory under '{physicalRoot}' cannot be read, so the handler"
                + $" configuration below it cannot be resolved: {failure.Message}", failure);
        }

        found.Sort(static (left, right) =>
        {
            var depth = left.Depth.CompareTo(right.Depth);
            return depth != 0 ? depth : DirectoryOrder.NameComparer.Compare(left.Key, right.Key);
        });

        for (var index = 1; index < found.Count; index++)
        {
            if (string.Equals(found[index - 1].Key, found[index].Key, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigurationErrorsException(
                    "'" + found[index - 1].ConfigPath + "' and '" + found[index].ConfigPath
                    + "' both configure the path '" + found[index].Key
                    + "'. Configuration paths ignore case, so only a case-sensitive filesystem can"
                    + " hold both and neither can be chosen. Rename one directory.");
            }
        }

        return found;
    }

    private readonly struct FolderConfig(string key, int depth, string configPath)
    {
        internal string Key { get; } = key;

        internal int Depth { get; } = depth;

        internal string ConfigPath { get; } = configPath;
    }

    private sealed class Record(IisRegistrationSection section, bool waiver)
    {
        internal IisRegistrationSection Section { get; } = section;

        internal bool Waiver { get; } = waiver;
    }
}
