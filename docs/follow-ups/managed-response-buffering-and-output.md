# Managed response buffering and output

Status: open. Priority: highest. First-slice contract:
[spooled asynchronous commit](../adr/0036-spool-first-slice-responses-before-async-commit.md).

## Problem

The first-slice worker request needs deterministic status, headers, memory body
fragments, logical flush state, and final completion without synchronous
Kestrel writes. Full `HttpWriter` portability remains broader work.

## Required decisions

- Per-request memory-to-work-file spool ownership and cleanup.
- Fragment ordering and logical first-flush/header state.
- Seal on `EndOfRequest`, then asynchronous Kestrel commit.
- Managed versus adapter failure propagation.
- Explicit first-slice rejection for file send and client-visible streaming.

## Verification

Focused tests cover memory and spill paths, multiple logical flushes, empty
output, status/header ordering, disconnect, commit failure, and cleanup.
Encoding, file ranges, and client-visible streaming belong to later gates.

## Done when

The first-slice response is deterministic, leak-free, completes exactly once,
and commits without enabling Kestrel synchronous I/O.
