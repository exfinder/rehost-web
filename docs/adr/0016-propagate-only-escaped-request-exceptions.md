---
status: accepted
---

# Propagate only exceptions that escape HttpRuntime

Rehost does not alter `HttpRuntime` to rethrow exceptions it normally owns.
System.Web retains `Application_Error`, `EndRequest`, custom-error formatting,
and normal `EndOfRequest` behavior.

If `HttpRuntime.ProcessRequest` itself escapes synchronously before normal
completion, the worker request records that exception as its terminal failure.
Awaiting host-side completion then propagates the same exception once through
ASP.NET Core error handling.
