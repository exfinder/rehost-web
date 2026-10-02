# Container memory validation

Synthetic monitor tests cannot establish the actual readings and reclamation a
control group provides. Routine platform rounds impose no memory limit.

## Open contract

- Verify total-limit resolution under cgroup v1 and v2; a GC heap ceiling alone
  does not represent physical/container memory pressure.
- Compare WorkingSet with memory.current including page-cache growth, and verify
  recycle notification arrives before kernel termination.
- Sustain cache pressure under a 512 MiB limit; collection must decommit pages
  rather than merely make objects collectible.
- Run beyond the monitor's polling interval to exercise callbacks and failures.
- Bound monitoring/trim CPU cost independently of memory limits.

A published ScenarioHost under `docker run --memory=512m` should assert observed
runtime outcomes. Choose a dedicated job alongside existing Linux CI and a Windows
execution route. [Runtime metrics](portable-runtime-metrics.md) can expose readings.
