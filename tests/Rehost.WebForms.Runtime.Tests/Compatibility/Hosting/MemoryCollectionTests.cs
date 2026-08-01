using System.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

// Isolated so that inducing a blocking collection does not perturb the timing of tests running in
// parallel, and so that their allocations do not perturb this one's measurements.
[CollectionDefinition(nameof(MemoryCollectionTests), DisableParallelization = true)]
public sealed class MemoryCollectionTestsCollection
{
}

[Collection(nameof(MemoryCollectionTests))]
public sealed class MemoryCollectionTests
{
    private const int AllocatedMiB = 100;

    // Trimming the cache only converts entries into garbage; the memory a container counts is not
    // returned until the collector decommits it. Measured across both platforms this recovers
    // 99.3-100%, where a plain GC.Collect() recovered 15% in a container.
    [Fact]
    public void An_Induced_Collection_Returns_Committed_Memory_To_The_Operating_System()
    {
        MemoryCollection.Induce();
        var baseline = GC.GetGCMemoryInfo().TotalCommittedBytes;

        var held = new List<byte[]>(AllocatedMiB);
        for (var i = 0; i < AllocatedMiB; i++)
        {
            var block = new byte[1024 * 1024];
            for (var offset = 0; offset < block.Length; offset += 4096)
            {
                block[offset] = 1;
            }

            held.Add(block);
        }

        var peak = GC.GetGCMemoryInfo().TotalCommittedBytes;
        GC.KeepAlive(held);
        held.Clear();

        MemoryCollection.Induce();
        var after = GC.GetGCMemoryInfo().TotalCommittedBytes;

        peak.ShouldBeGreaterThan(baseline, "the allocation should have committed memory");

        var recovered = (double)(peak - after) / (peak - baseline);
        recovered.ShouldBeGreaterThan(0.5, $"baseline={baseline} peak={peak} after={after}");
    }
}
