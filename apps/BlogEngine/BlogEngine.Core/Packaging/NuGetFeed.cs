using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace BlogEngine.Core.Packaging
{
    internal static class NuGetFeed
    {
        const int PageSize = 1000;

        static SourceRepository Repository()
        {
            return NuGet.Protocol.Core.Types.Repository.Factory.GetCoreV3(BlogConfig.GalleryFeedUrl);
        }

        // Callers run on the request's synchronization context; waiting there deadlocks.
        static T Run<T>(Func<Task<T>> call)
        {
            return Task.Run(call).GetAwaiter().GetResult();
        }

        public static List<IPackageSearchMetadata> LatestPackages()
        {
            return Run(async () =>
            {
                var search = await Repository().GetResourceAsync<PackageSearchResource>().ConfigureAwait(false);
                var results = await search.SearchAsync(
                    string.Empty,
                    new SearchFilter(includePrerelease: false),
                    0,
                    PageSize,
                    NullLogger.Instance,
                    CancellationToken.None).ConfigureAwait(false);
                return results.ToList();
            });
        }

        public static IPackageSearchMetadata FindLatest(string packageId)
        {
            return Run(async () =>
            {
                var repository = Repository();
                using (var cache = new SourceCacheContext())
                {
                    var find = await repository.GetResourceAsync<FindPackageByIdResource>().ConfigureAwait(false);
                    var versions = await find.GetAllVersionsAsync(packageId, cache, NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);
                    var latest = versions?.Where(v => !v.IsPrerelease).DefaultIfEmpty().Max();
                    if (latest == null)
                        return null;

                    var metadata = await repository.GetResourceAsync<PackageMetadataResource>().ConfigureAwait(false);
                    return await metadata.GetMetadataAsync(new PackageIdentity(packageId, latest), cache, NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);
                }
            });
        }

        public static void Extract(PackageIdentity identity, string directory)
        {
            Run(async () =>
            {
                using (var cache = new SourceCacheContext())
                using (var nupkg = new MemoryStream())
                {
                    var find = await Repository().GetResourceAsync<FindPackageByIdResource>().ConfigureAwait(false);
                    var found = await find.CopyNupkgToStreamAsync(identity.Id, identity.Version, nupkg, cache, NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);
                    if (!found)
                        throw new InvalidOperationException($"Package {identity} was not found in {BlogConfig.GalleryFeedUrl}");

                    nupkg.Position = 0;
                    using (var reader = new PackageArchiveReader(nupkg))
                    {
                        foreach (var entry in reader.GetFiles().Where(IsPayload))
                        {
                            var target = Path.GetFullPath(Path.Combine(directory, entry));
                            if (!target.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                                throw new InvalidOperationException($"Package {identity} has an entry outside its folder: {entry}");

                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            using (var source = reader.GetStream(entry))
                            using (var file = File.Create(target))
                                await source.CopyToAsync(file).ConfigureAwait(false);
                        }
                    }
                }
                return true;
            });
        }

        public static void Remove(string packageId, string packagesRoot)
        {
            if (!Directory.Exists(packagesRoot))
                return;

            var prefix = packageId + ".";
            foreach (var dir in Directory.GetDirectories(packagesRoot))
            {
                var name = Path.GetFileName(dir);
                // "Foo.1.0.0" belongs to Foo; "Foo.Bar.1.0.0" does not.
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && NuGetVersion.TryParse(name.Substring(prefix.Length), out _))
                {
                    Directory.Delete(dir, true);
                }
            }
        }

        static bool IsPayload(string entry)
        {
            return !entry.StartsWith("_rels/", StringComparison.OrdinalIgnoreCase)
                && !entry.StartsWith("package/", StringComparison.OrdinalIgnoreCase)
                && !entry.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase);
        }
    }
}
