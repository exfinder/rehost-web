#nullable enable

namespace System.Web.Hosting;

using System;

internal static class MemoryCollection
{
    // Measured in a container: a plain collection returned 31 MB of 200 MB trimmed, because it
    // leaves the pages committed and a cgroup counts committed pages. Aggressive decommits.
    internal static void Induce()
    {
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }
}
