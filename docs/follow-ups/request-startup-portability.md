# Request-startup portability

Status: open. Priority: highest. Canonical execution:
[managed runtime port plan](../core-runtime-port-plan.md).

## Problem

The first `HttpRuntime` request transitively reaches IIS/native operations:
engine discovery, health initialization, request-queue tuning, processor and
module version lookup, account lookup, file monitors, and native prefetch.
Blanket native failure or scattered platform guards do not define behavior.

Supported state flow and lifetimes:
[classic managed runtime model](../classic-managed-runtime-model.md).
Incremental classification:
[portability ledger](../portability-ledger.md).

## Scope

Own portable `HttpRuntime` initialization from its static constructor through
`StaticInit`, instance `Init`, `HostingInit`, and first request dispatch.
Consume application identity, roots, work storage, configuration mapping,
full-trust policy, and lifecycle state from the process-scoped application
owner. The accepted bootstrap contract is a target, not a completed
prerequisite.

Split managed runtime initialization from optional IIS/native integration.
Registry, IIS, assembly-location, Windows identity, native monitoring, and
secondary-AppDomain discovery cannot participate in the supported path.

Process-wide tuning belongs to
[runtime process policy](runtime-process-policy.md). Shutdown, recycle, and
unload behavior belongs to
[process lifetime](process-lifetime-shutdown-and-recycle.md).

## Current boundary

The portable harness reaches `HttpRuntime.ProcessRequest` on macOS and Windows;
the [ledger](../portability-ledger.md) owns reached-edge evidence.

Still open:

- module, handler, and response parity are **not** claimed; the trace currently
  records only request entry, `EndOfRequest`, and return;
- worker identity meaning remains research;
- generation-specific work storage remains open — see
  [runtime codegen and loading](runtime-codegen-and-loading.md);
- machine keys carry a process-scoped deviation — see
  [machine key and ViewState bootstrap](machine-key-and-viewstate-bootstrap.md);
- explicit rejection of `<identity impersonate="true"/>` is deferred to the
  identity slice; the subsystem is currently inert.

## Required decisions

Trace only edges reached by the current executable slice. Classify each as:

- portable semantic replacement;
- host lifecycle/service responsibility;
- explicitly unsupported feature with early diagnostic; or
- deferred or proven unreachable imported code.

No supported path may select different capability merely because the OS is
Windows.

## Verification

Add focused tests at each new seam. The final integration test runs with native
IIS libraries unavailable and asserts no reachable P/Invoke.

## Done when

The bodyless precompiled-handler path is classified, documented, and portable.
No supported edge reaches native IIS/Windows facilities, and no inactive
behavior exists without an explicit postcondition and probe.
