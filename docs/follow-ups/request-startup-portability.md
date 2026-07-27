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
owner. The bootstrap commit is input to reshape, not a completed prerequisite.

Split managed runtime initialization from optional IIS/native integration.
Registry, IIS, assembly-location, Windows identity, native monitoring, and
secondary-AppDomain discovery cannot participate in the supported path.

Process-wide tuning belongs to
[runtime process policy](runtime-process-policy.md). Shutdown, recycle, and
unload behavior belongs to
[process lifetime](process-lifetime-shutdown-and-recycle.md).

## Progress

Request ownership now transfers. `PortableParity.Host run` completes the public
sequence — `WebFormsApplication.Initialize`, `ApplicationManager.GetApplicationManager`,
`Open`, `CreateObject`, `HostingEnvironment` initialization, portable runner —
and enters `HttpRuntime.ProcessRequest(HttpWorkerRequest)`, emitting a
`PipelineTrace` instead of an activation-phase `FailureDiagnostic`.

`HttpRuntime` static construction, `Init`, `HostingInit`, and
`BuildManager.InitializeBuildManager` all complete. Each edge reached is
classified in the [portability ledger](../portability-ledger.md) as P01–P24.

Still open:

- module, handler, and response parity are **not** claimed; the trace currently
  records only request entry, `EndOfRequest`, and return;
- H03 (worker identity meaning) remains research;
- H10 is only partially met — see
  [runtime codegen and loading](runtime-codegen-and-loading.md);
- H16 carries an explicit process-scoped key deviation — see
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
