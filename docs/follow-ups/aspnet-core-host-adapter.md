# ASP.NET Core host adapter

Status: open. Priority: high. Depends on host-neutral boundary.

## Problem

Kestrel must enter System.Web through `HttpWorkerRequest` without importing IIS
hosting assumptions. Request paths, query, method, protocol, addresses, known
headers, server variables, and response operations need explicit mappings.

## Required decisions

- Assembly/package boundary and public host-registration API.
- Exact meaning of app path, file path, path info, raw URL, and translated path.
- Minimal server-variable set and behavior for IIS-only variables.
- Ownership of synchronous bridging required by the legacy contract.
- Routing/pass-through rule for the first `.aspx` surface.
- Which worker-request methods fail explicitly until later stories.

Do not inherit POC mappings without checking System.Web consumers and ASP.NET
Core semantics.

## Verification

Unit-test each first-request mapping, including encoded paths, repeated headers,
missing connection data, virtual-root hosting, and unsupported members.

## Done when

The adapter exposes the minimum GET contract without IIS/native state, unsafe
path construction, or silent placeholder values.
