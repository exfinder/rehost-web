# Portable runtime metrics

## Current state

`PerfCounters` writes about ninety counters into shared memory owned by
`webengine4.dll`. `OpenCounter` returns immediately because `HttpRuntime`
`IsEngineLoaded` is permanently false (P04), so the instance, global and state
handles are never populated and every write is guarded behind a null check —
including `PerfInstanceDataHandle.ReleaseHandle`, which can only run on a handle
that was never created. `UnsafeNativeMethods.PerfCounterInitialize` is inside
`#if NETFRAMEWORK`.

The class is therefore inert on every platform and reaches no native code. It
blocks nothing, which is why it was left alone while the memory monitors were
ported.

`AppDomainResourcePerfCounters` is inert for the same reason but not idle: it
polls `AppDomain.MonitoringSurvivedMemorySize` and `MonitoringTotalProcessorTime`
every five seconds — both work on all three platforms — and feeds the result to
writes that discard it.

## What is lost

Nothing observes cache size, cache hit ratio, requests queued, requests
executing, request wait time, memory pressure, or application memory and CPU.
The two memory monitors compute several of these on every poll and discard them.

## Shape

`System.Diagnostics.Metrics` behind the existing `IPerfCounters` seam. All
ninety counters pass through four methods keyed by `AppPerfCounter`, so the work
is one mapping table rather than ninety call sites. Deciding it needs:

- whether the meter is on by default, given that a meter with no listener costs
  almost nothing but is behaviour Framework did not have;
- names, which should follow OpenTelemetry conventions rather than transliterate
  the Windows counter names;
- the raw-fraction pairs. `CACHE_PERCENT_PROC_MEM_LIMIT_USED` and its `_BASE`
  partner are a value and a denominator, in kilobytes because the counter is a
  32-bit integer. Two gauges in bytes state the same thing honestly; copying the
  pair across would preserve an encoding that only perfmon needed.

Whether `AppDomainResourcePerfCounters` keeps polling in the meantime is open.
It is harmless but pointless while its output is discarded.

Related: [container memory validation](container-memory-validation.md).
