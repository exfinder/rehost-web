# Request-startup portability

Status: open. Priority: high. Depends on application bootstrap and
supported-path reachability.

## Problem

The first `HttpRuntime` request transitively reaches IIS/native operations:
engine discovery, health initialization, request-queue tuning, processor and
module version lookup, account lookup, file monitors, and native prefetch.
Blanket native failure or scattered platform guards do not define behavior.

Legacy state flow and phase postconditions:
[IIS integrated initialization research](../research/aspnet-iis-integrated-initialization-pipeline.md).

Proposed execution:
[HttpRuntime portability plan](../http-runtime-porting-plan.md).

## Scope

Own portable `HttpRuntime` initialization from its static constructor through
`StaticInit`, instance `Init`, `HostingInit`, and first request dispatch.
Consume application identity, roots, configuration, full-trust policy, and
lifecycle state exclusively from the completed
[application bootstrap](../application-bootstrap-and-configuration.md).

Split managed runtime initialization from optional IIS/native integration.
Registry, IIS, assembly-location, Windows identity, native monitoring, and
secondary-AppDomain discovery cannot participate in the supported path.

Process-wide tuning belongs to
[runtime process policy](runtime-process-policy.md). Shutdown, recycle, and
unload behavior belongs to
[process lifetime](process-lifetime-shutdown-and-recycle.md).

## Required decisions

Trace the exact first-request graph. Classify each dependency as:

- portable semantic replacement;
- host lifecycle/service responsibility;
- explicitly unsupported feature with early diagnostic; or
- proven unreachable imported code.

No supported path may select different capability merely because the OS is
Windows.

## Verification

Add focused tests at each new seam. The final integration test runs with native
IIS libraries unavailable and asserts no reachable P/Invoke.

## Done when

The entire `HttpRuntime` initialization and first-request startup graph is
classified, documented, and portable. Initialization consumes only the
bootstrap contract, no supported path reaches native IIS/Windows facilities,
and no no-op exists without an approved semantic contract.
