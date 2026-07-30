# Dynamic ASPX integration

Status: open. Priority: high. Slice 3; depends on the dynamic-startup slice.

This slice also carries slice 2's differential gate. Its fixture cannot render
without pre-application start, `App_Code`, `Global.asax`, and
`Application_Start`, so matching the Framework golden here ratifies the ordering
that [ADR 0043](../adr/0043-gate-the-compilation-substrate-locally.md) deferred.

## Fixture

One application with `Default.aspx`, C# code-behind, and `web.config`.
`GET /Default.aspx?value=...` renders deterministic HTML containing a value
produced by code-behind. No precompiled page assembly.

## Test

Start real Kestrel on Linux, request the page, and assert:

- completion within a bounded test timeout;
- expected status and selected headers;
- expected encoded body and code-behind output;
- dynamic generated assembly creation/loading;
- no native IIS/Windows dependency reached;
- clean shutdown with no background failure.

This test closes explicitly deferred verification from intermediary stories
and has a matching .NET Framework core differential fixture. Kestrel transport
remains a separate integration assertion.

## Done when

The test passes repeatedly from disposable application/codegen roots and every
remaining exclusion is represented in the compatibility feature map.
