# Route URL escaping

## Problem

Outbound routing uses obsolete `Uri.EscapeUriString`. Direct replacement with
`EscapeDataString` changes slash, percent-escape, Unicode, invalid-input, and
catch-all behavior.

## Required evidence

Compare Framework and portable candidates for literals, parameters, catch-all
slashes, existing escapes, literal percent, Unicode/invalid surrogates, long
inputs, nested routes, and inbound round trips.

Candidate: encode values as data, preserving only path separators required by
catch-all semantics.

## Done when

- Expected encoding is specified and covered by differential tests.
- Obsolete encoding is replaced or narrowly retained with justification.
- Temporary `SYSLIB0013` suppression is removed.
