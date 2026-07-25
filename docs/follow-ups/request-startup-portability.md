# Request-startup portability

Status: open. Priority: high. Depends on supported-path reachability.

## Problem

The first `HttpRuntime` request transitively reaches IIS/native operations:
engine discovery, health initialization, request-queue tuning, processor and
module version lookup, account lookup, file monitors, and native prefetch.
Blanket native failure or scattered platform guards do not define behavior.

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

The entire first-request startup graph is classified, documented, and portable;
no no-op exists without an approved semantic contract.
