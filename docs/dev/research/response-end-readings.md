# Response.End termination readings

Evidence for how a request ends when application or library code catches the
unwind that `Response.End`, or a `Response.Redirect` that ends the response,
starts. RT1-RT7 were observed on IIS Express 10.0.26013 on `winbox`, .NET
Framework 4.8 with `System.Web` 4.8.9344, integrated mode, 2026-09-28.

## Method

The [Web Pages readings](webpages-readings.md) site, with
`Microsoft.AspNet.WebPages` 3.3.0 in `bin`. `Global.asax` appends
`<RawUrl> <event>` to `App_Data/events.txt` from `Application_Error` (with
the error's type and its inner exception's type),
`Application_PostRequestHandlerExecute` and `Application_EndRequest`. The
pages:

- `error.ashx` throws `InvalidOperationException("real")`.
- `wrap.ashx` writes `before|`, calls `Response.End()` in a `try`, and its
  `catch (Exception e)` throws `new InvalidOperationException("wrapped", e)`.
- `rethrow.ashx` is `wrap.ashx` with `throw new Exception("failed")`, which
  drops the caught exception.
- `swallow.ashx` writes `before|` and calls `Response.End()` in a `try` whose
  `catch (Exception e)` writes `in-catch:` and the caught type's name; after
  the `try` it writes `after-try|`.
- `zz-redirect.cshtml` is `@{ Response.Redirect("~/"); }`, and
  `zz-redirect-noend.cshtml` passes `false` for `endResponse`.
- `wrap.aspx` does what `wrap.ashx` does from `Page_Load`, and its
  `Page_Error` appends `Page_Error` and the error's type to the same file.

Requests used on-box `curl.exe -s -i`. The same pages ran on the port's
scratch build on macOS arm64 before the fix below, in the last column; RT7
was read on Framework and on the port after the fix.

| ID | Page | Framework 4.8 | Port before the fix |
| --- | --- | --- | --- |
| RT1 | `error.ashx` (control) | 500; `Application_Error InvalidOperationException`, `EndRequest` | Same |
| RT2 | `zz-redirect.cshtml` | 302, `Location: /`, the "Object moved" body; only `EndRequest` | 500; `Application_Error HttpUnhandledException inner=CancelModuleException`, `EndRequest` |
| RT3 | `zz-redirect-noend.cshtml` | 302; `PostRequestHandlerExecute`, `EndRequest` | Same |
| RT4 | `wrap.ashx` | 200, body `before\|`; only `EndRequest` | 500; `Application_Error InvalidOperationException inner=CancelModuleException`, `EndRequest` |
| RT5 | `rethrow.ashx` | 200, body `before\|`; only `EndRequest` | 500; `Application_Error Exception` with no inner exception, `EndRequest` |
| RT6 | `swallow.ashx` | 200, body `before\|in-catch:ThreadAbortException\|`; only `EndRequest` | 200, body `before\|`; only `EndRequest` |
| RT7 | `wrap.aspx` | 200, body `before\|`; `Page_Error InvalidOperationException`, then only `EndRequest` | Not read before the fix; after it, the same as Framework |

On Framework the thread abort re-raised itself at the end of every catch
block, and a handler's was reset only in `HttpApplication.ExecuteStep`, so
neither the exception a catch threw in its place nor the
`HttpUnhandledException` that Web Pages wraps around the abort reached
`Application_Error`, and nothing after the swallowing catch ran.

## The port now

`Response.End` marks the request as terminating before it unwinds, and an
exception that escapes the pipeline step while the mark is set ends the
request as the termination, whatever it wraps
([ledger P52](../portability-ledger.md)). RT1-RT5 and RT7 match Framework; a
Web Forms page's own `Page_Error` still sees the exception first, as on
Framework. RT6 keeps
its status and events, and one difference in the body: the port seals the
response at `Response.End`, so the catch's `in-catch:` output, which
Framework sent, is dropped. The code after the catch runs until the step
ends, where on Framework it never ran; its output is dropped too.
