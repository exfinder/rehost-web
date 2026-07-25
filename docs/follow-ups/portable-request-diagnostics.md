# Portable request diagnostics

Status: open. Priority: high. Depends on request pipeline.

## Problem

Startup, compilation, and request errors can enter Windows Event Log, WMI,
native web-event, or IIS trace paths while handling the original failure.
Diagnostics must not mask errors or introduce a Windows-only supported path.

## Required decisions

- Host-neutral logging/event boundary and required structured fields.
- Error-page behavior before and after headers are sent.
- Compilation/startup exception preservation.
- Policy for legacy health-monitoring providers and IIS trace APIs.
- Sensitive-data redaction.

## Verification

Tests inject bootstrap, compilation, handler, and response failures; assert the
original error remains observable and diagnostic sinks cannot recursively fail
the request.

## Done when

First-request failures produce portable actionable diagnostics without Event
Log, WMI, IIS trace, or native web-event dependencies.
