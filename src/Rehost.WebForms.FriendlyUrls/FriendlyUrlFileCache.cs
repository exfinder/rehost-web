using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;

namespace Microsoft.AspNet.FriendlyUrls;

internal sealed class FriendlyUrlFileCache
{
    private static long _nextId;

    private readonly ResolverCachingMode _mode;
    private readonly VirtualPathProvider _provider;
    private readonly IFriendlyUrlCacheStore _cache;
    private readonly string _cacheKeyPrefix;
    private readonly Lazy<HashSet<string>?> _staticFiles;

    internal FriendlyUrlFileCache(
        ResolverCachingMode mode,
        VirtualPathProvider provider,
        string applicationVirtualPath,
        IFriendlyUrlCacheStore cache)
    {
        _mode = mode;
        _provider = provider;
        _cache = cache;
        _cacheKeyPrefix = "__FriendlyUrls_File_" + Interlocked.Increment(ref _nextId) + "_";
        _staticFiles = new Lazy<HashSet<string>?>(
            () => ReadApplicationFiles(applicationVirtualPath),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    internal bool FileExists(string virtualPath)
    {
        return _mode switch
        {
            ResolverCachingMode.Static => StaticFileExists(virtualPath),
            ResolverCachingMode.Dynamic => DynamicFileExists(virtualPath),
            _ => _provider.FileExists(virtualPath)
        };
    }

    private bool StaticFileExists(string virtualPath)
    {
        var files = _staticFiles.Value;
        var absolutePath = VirtualPathUtility.ToAbsolute(virtualPath);
        return files?.Contains(absolutePath) ?? _provider.FileExists(virtualPath);
    }

    private bool DynamicFileExists(string virtualPath)
    {
        var cacheKey = _cacheKeyPrefix + (_provider.GetCacheKey(virtualPath) ?? virtualPath);
        if (_cache.Get(cacheKey) is bool cached)
        {
            return cached;
        }

        var utcStart = DateTime.UtcNow;
        var exists = _provider.FileExists(virtualPath);
        var dependency = _provider.GetCacheDependency(
            virtualPath,
            new[] { virtualPath },
            utcStart);
        if (dependency != null)
        {
            _cache.Insert(cacheKey, exists, dependency);
        }

        return exists;
    }

    private HashSet<string>? ReadApplicationFiles(string applicationVirtualPath)
    {
        var root = _provider.GetDirectory(applicationVirtualPath);
        if (root == null)
        {
            return null;
        }

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<VirtualDirectory>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Push(root);

        while (pending.Count != 0)
        {
            var directory = pending.Pop();
            if (!visited.Add(directory.VirtualPath))
            {
                continue;
            }

            foreach (var file in directory.Files.OfType<VirtualFile>())
            {
                files.Add(file.VirtualPath);
            }

            foreach (var child in directory.Directories.OfType<VirtualDirectory>())
            {
                pending.Push(child);
            }
        }

        return files;
    }
}

internal interface IFriendlyUrlCacheStore
{
    object? Get(string key);

    void Insert(string key, object value, CacheDependency dependency);
}

internal sealed class HttpRuntimeCacheStore : IFriendlyUrlCacheStore
{
    internal static HttpRuntimeCacheStore Instance { get; } = new();

    public object? Get(string key)
    {
        return HttpRuntime.Cache[key];
    }

    public void Insert(string key, object value, CacheDependency dependency)
    {
        HttpRuntime.Cache.Insert(key, value, dependency);
    }
}
