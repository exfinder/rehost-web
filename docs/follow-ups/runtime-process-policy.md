# Runtime process policy

Status: open. Priority: high. Depends on host lifecycle.

## Problem

`HttpRuntime` configures thread-pool limits and monitors process/machine memory
using IIS-era policy and Windows APIs. Silently skipping configuration or
substituting container thresholds changes scheduling, overload, cache trimming,
and recycle behavior.

## Required decisions

- Runtime versus host ownership of thread-pool configuration.
- Request admission/queue policy for the first milestone.
- Memory-pressure observation and container-limit semantics.
- Cache trimming, overload, recycle, and unsupported configuration behavior.
- Which policy is required before one request versus deliberately deferred.

## Verification

Test configuration interpretation and startup reachability. Load/recycle tests
may defer to process-lifetime work but must be named precisely.

## Done when

One request cannot mutate global process policy unexpectedly; deferred process
features are explicit rather than no-op.
