using System.Collections;
using System.Web.Caching;
using System.Web.Hosting;
using Microsoft.AspNet.FriendlyUrls;
using Shouldly;
using Xunit;

namespace Rehost.AspNet.FriendlyUrls.Tests.Caching;

public sealed class FriendlyUrlFileCacheTests
{
    [Fact]
    public void StaticModeKeepsFirstApplicationSnapshot()
    {
        var provider = new MutableVirtualPathProvider("/About.aspx");
        var cache = new FriendlyUrlFileCache(
            ResolverCachingMode.Static,
            provider,
            "/",
            new TestCacheStore());

        cache.FileExists("/About.aspx").ShouldBeTrue();
        provider.Add("/Contact.aspx");

        cache.FileExists("/Contact.aspx").ShouldBeFalse();
        provider.FileExistsCallCount.ShouldBe(0);
    }

    [Fact]
    public void DynamicModeCachesUntilProviderDependencyChanges()
    {
        var provider = new MutableVirtualPathProvider("/About.aspx");
        var cache = new FriendlyUrlFileCache(
            ResolverCachingMode.Dynamic,
            provider,
            "/",
            new TestCacheStore());

        cache.FileExists("/About.aspx").ShouldBeTrue();
        cache.FileExists("/About.aspx").ShouldBeTrue();
        provider.FileExistsCallCount.ShouldBe(1);

        provider.Remove("/About.aspx");

        cache.FileExists("/About.aspx").ShouldBeFalse();
        provider.FileExistsCallCount.ShouldBe(2);
    }

    [Fact]
    public void DisabledModeAlwaysConsultsProvider()
    {
        var provider = new MutableVirtualPathProvider("/About.aspx");
        var cache = new FriendlyUrlFileCache(
            ResolverCachingMode.Disabled,
            provider,
            "/",
            new TestCacheStore());

        cache.FileExists("/About.aspx").ShouldBeTrue();
        cache.FileExists("/About.aspx").ShouldBeTrue();

        provider.FileExistsCallCount.ShouldBe(2);
    }

    private sealed class MutableVirtualPathProvider(params string[] files) : VirtualPathProvider
    {
        private readonly HashSet<string> _files = new(files, StringComparer.OrdinalIgnoreCase);
        private readonly List<MutableCacheDependency> _dependencies = [];

        internal int FileExistsCallCount { get; private set; }

        public override bool FileExists(string virtualPath)
        {
            FileExistsCallCount++;
            return _files.Contains(virtualPath);
        }

        public override VirtualDirectory? GetDirectory(string virtualDir)
        {
            return string.Equals(virtualDir, "/", StringComparison.Ordinal)
                ? new TestVirtualDirectory(virtualDir, _files)
                : null;
        }

        public override CacheDependency GetCacheDependency(
            string virtualPath,
            IEnumerable virtualPathDependencies,
            DateTime utcStart)
        {
            var dependency = new MutableCacheDependency();
            _dependencies.Add(dependency);
            return dependency;
        }

        internal void Add(string virtualPath)
        {
            _files.Add(virtualPath);
            InvalidateDependencies();
        }

        internal void Remove(string virtualPath)
        {
            _files.Remove(virtualPath);
            InvalidateDependencies();
        }

        private void InvalidateDependencies()
        {
            foreach (var dependency in _dependencies)
            {
                dependency.Invalidate();
            }

            _dependencies.Clear();
        }
    }

    private sealed class TestCacheStore : IFriendlyUrlCacheStore
    {
        private readonly Dictionary<string, Entry> _entries = [];

        public object? Get(string key)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                return null;
            }

            if (entry.Dependency.HasChanged)
            {
                _entries.Remove(key);
                return null;
            }

            return entry.Value;
        }

        public void Insert(string key, object value, CacheDependency dependency)
        {
            _entries[key] = new Entry(value, dependency);
        }

        private sealed record Entry(object Value, CacheDependency Dependency);
    }

    private sealed class MutableCacheDependency : CacheDependency
    {
        internal MutableCacheDependency()
        {
            FinishInit();
        }

        internal void Invalidate()
        {
            NotifyDependencyChanged(this, EventArgs.Empty);
        }
    }

    private sealed class TestVirtualDirectory(
        string virtualPath,
        IEnumerable<string> files) : VirtualDirectory(virtualPath)
    {
        private readonly VirtualFile[] _files = files
            .Select(file => new TestVirtualFile(file))
            .ToArray();

        public override IEnumerable Directories => Array.Empty<VirtualDirectory>();

        public override IEnumerable Files => _files;

        public override IEnumerable Children => _files;
    }

    private sealed class TestVirtualFile(string virtualPath) : VirtualFile(virtualPath)
    {
        public override Stream Open()
        {
            return Stream.Null;
        }
    }
}
