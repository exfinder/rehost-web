---
status: accepted
---

# Preserve Framework sequencing through surgical ports

The imported Framework startup and request call graph remains the behavioral
skeleton. Portability changes are made at incompatible leaves, preferring
existing configuration or policy switches that produce a valid disabled state.

Broad rewrites of `HostingEnvironment`, `HttpRuntime`, and their managed
sequencing require prior differential characterization. Skipping a call is not
equivalent to disabling its subsystem: all downstream state postconditions must
still hold.

For each incompatible subsystem, use this order:

1. Framework-supported disabled configuration;
2. existing capability-driven inactive behavior;
3. surgical portable leaf replacement preserving postconditions;
4. explicit unsupported failure.

Lifecycle callers and phases remain intact.
