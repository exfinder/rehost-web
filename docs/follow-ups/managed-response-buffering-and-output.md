# Managed response buffering and output

Status: open. Priority: high. Depends on host adapter.

## Problem

Classic `HttpWriter` allocates and transmits unmanaged response elements.
Portable output must retain append, encoding, clone, recycle, ordering, header,
status, file-send, flush, and finalization semantics. A pooled byte array
introduces ownership and use-after-return risks.

## Required decisions

- Managed buffer ownership, growth, pooling, clearing, and recycling.
- Character encoding and encoder flush behavior.
- Fragment ordering across memory and file output.
- Status/header mutation and first-flush rules.
- Filename-based send; policy for native-handle send.
- Write-failure and client-disconnect propagation.

## Verification

Focused tests cover split multibyte characters, multiple flushes, clone/recycle,
buffer reuse, file ranges, empty output, header timing, and write failures.

## Done when

The first page response is fully managed, deterministic, leak-free, and
compatible with the worker-request contract.
