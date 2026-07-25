# Dynamic ASPX integration

Status: open. Priority: high. Depends on all first-runnable-request
implementation stories.

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

This test closes explicitly deferred verification from intermediary stories.
It is not a .NET Framework differential test.

## Done when

The test passes repeatedly from disposable application/codegen roots and every
remaining exclusion is represented in the compatibility feature map.
