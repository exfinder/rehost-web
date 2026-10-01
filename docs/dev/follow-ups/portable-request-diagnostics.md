# Portable request diagnostics

## Problem

Startup, compilation, and request errors can enter Windows Event Log, WMI,
native web-event, or IIS trace paths while handling the original failure.
Diagnostics must not mask errors or introduce a Windows-only supported path.

## Settled

[ADR 0011](../adr/0011-portable-diagnostics-boundary.md) fixed the host-neutral
boundary: one choke point publishing every event on the `EventSource` and on
`ILogger` under the single category `Rehost.Web`, a never-throw
wrapper, request errors delivered above the `healthMonitoring` gate, and
compilation and startup failures carrying their live exception. Legacy
health-monitoring providers and IIS trace APIs stay unsupported; the managed
provider model remains backlog.

## Required decisions

- Error-page behavior before and after headers are sent.
- Sensitive-data redaction.

## Verification

Tests inject bootstrap, compilation, handler, and response failures; assert the
original error remains observable and diagnostic sinks cannot recursively fail
the request.

## Done when

First-request failures produce portable actionable diagnostics without Event
Log, WMI, IIS trace, or native web-event dependencies.
