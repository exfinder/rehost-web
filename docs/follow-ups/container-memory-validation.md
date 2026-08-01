# Container memory validation

Status: deferred. The memory monitors are ported and unit-covered; nothing in the
suite exercises them against a real control group.

## Why the existing gate is not enough

Both supported platforms run without a memory limit imposed on themselves, and
without one `TotalAvailableMemoryBytes` happens to equal physical RAM. The defect
recorded in P17 and P21 — load overstated by a third — is therefore invisible on
macOS and Windows alike, and a full green run on both would not have found it.

`MemoryLimitsTests` reaches those shapes with synthetic readings, which proves
the arithmetic but assumes the readings a container actually produces. That
assumption is the part still unverified.

The soak that reproduced P44 — an activated application held open past the
monitor's first 30-second tick — is also unautomated, because it costs longer
than the whole suite. Any test that would have caught that crash has to outlive
the poll interval.

## What only a control group can establish

- `MemoryLimits.ResolveTotalLimit` equals the declared limit, across cgroup v2
  (`memory.max`) and v1 (`memory.limit_in_bytes`); Amazon Linux 2 is still v1.
- `Environment.WorkingSet` tracks `memory.current` closely enough that the
  recycle monitor fires before the kernel does. Measured 28.4 MB against
  31.2 MB at rest; the gap is page cache and grows with file serving.
- An application under sustained cache pressure at 512 MiB survives rather than
  being killed, and the induced collection returns memory to the control group
  and not merely to the collector.
- The monitor's own poll and trim cost stays within the CPU quota, which is
  separate from the memory limit and is what `ComputeTrimLimit` does not model.

## Shape

`docker run --memory=512m` over a published `ScenarioHost`, asserting traced
values rather than timing. Establishing it means deciding where container tests
run, since the repository has no CI and the validation ritual in
[windows host](../windows-validation-host.md) is manual and Windows-only.

Related: [runtime metrics](portable-runtime-metrics.md), whose counters would
carry these measurements out of the process.
