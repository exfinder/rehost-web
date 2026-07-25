---
status: accepted
---

# Require the first-slice parity gate

The precompiled-handler slice is incomplete until differential and adapter
probes cover:

- cold synchronous success;
- concurrent cold requests with one initialization;
- warm concurrent requests with isolated `HttpContext` and pooled applications;
- a delayed asynchronous handler whose completion the adapter awaits;
- module `CompleteRequest`, skipped handler execution, and `EndRequest`;
- module and handler exceptions owned by System.Web error processing;
- handler-resolution or configuration failure; and
- exactly one terminal shutdown notification.
