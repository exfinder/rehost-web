# Response.End, Redirect, and request termination

Status: done 2026-08-05 (ledger P51, P52; `ResponseEndOverKestrelTests`). The
timeout policy is delivered cooperatively (ledger P53) under
[request termination and timeouts](request-termination-and-timeouts.md), and
`Server.Transfer`/`Execute` termination is recorded unassessed in the
compatibility map. Slice 4 capability story split from
[deferred request surfaces](deferred-request-surfaces.md); it owns the
termination half of [request termination and
timeouts](request-termination-and-timeouts.md), which stays open for the
timeout policy. Sequenced after the in-flight cookies story — see
[Sequencing](#sequencing). Carries a latent process-termination defect that
should not wait for the rest of the story; see [What the port does
today](#what-the-port-does-today). Mechanism and scope were decided in review —
see [Decisions](#decisions-2026-08-05).

## Problem

Classic ASP.NET terminates a request by aborting the request thread.
`Response.End` calls `Thread.CurrentThread.Abort`, `Response.Redirect(url)`
defaults to `endResponse: true` and rides the same call, and `executionTimeout`
aborts from a timer thread. On .NET 10 `Thread.Abort` and `Thread.ResetAbort`
throw `PlatformNotSupportedException: Thread abort is not supported on this
platform.` (`dotnet/runtime` `Thread.cs`, `SYSLIB0006`), so the mechanism has no
direct translation.

The mechanism is not an implementation detail applications are insulated from.
They depend on it by name: code after `End()` not running, `finally` blocks
running anyway, `catch (ThreadAbortException)` written literally, broad
`catch (Exception)` blocks that on Framework could not swallow it,
`Server.GetLastError` staying null, and `CompleteRequest` as the documented
non-aborting alternative. Recovering that intent — rather than substituting new
logic — is the compatibility policy's requirement.

## Recovered Framework contract

Citations are `System.Web/<file>:<line>` in the pinned Reference Source at
`../referencesource`, which the imported copy under
`src/System.Web.ReferenceSource` carries byte-identical for `HttpResponse.cs`,
`HttpApplication.cs`, `HttpContext.cs`, and `httpserverutility.cs`.

### The signal

`HttpResponse.End` (`HttpResponse.cs:3085`) has two arms and no third:

```csharp
public void End() {
    if (_context.IsInCancellablePeriod) {
        AbortCurrentThread();
    }
    else {
        // when cannot abort execution, flush and supress further output
        _endRequiresObservation = true;
        if (!_flushing) {
            Flush();
            _ended = true;
            if (_context.ApplicationInstance != null) {
                _context.ApplicationInstance.CompleteRequest();
            }
        }
    }
}
```

`AbortCurrentThread` (`HttpResponse.cs:3114`) is
`Thread.CurrentThread.Abort(new HttpApplication.CancelModuleException(false))`.
`CancelModuleException` (`HttpApplication.cs:2712`) is **not an exception** — it
is a plain object carried as the abort's `stateInfo` and read back through
`ThreadAbortException.ExceptionState`. Its single field distinguishes the two
callers: `false` is `Response.End`, `true` is the execution timeout
(`RequestTimeoutManager.cs:177`).

The non-cancellable arm matters more than its obscurity suggests. It is
Framework's own abort-free implementation of `End`, and Microsoft documents it
as part of the contract rather than as a fallback of last resort: *"To mimic the
behavior of the `End` method in ASP, this method **tries to** raise a
`ThreadAbortException`… If the `End` method is **not able** to raise a
`ThreadAbortException`, it instead flushes the response bytes to the client… In
either case… the response pipeline jumps ahead to the `EndRequest` event."*
(`HttpResponse.End` API remarks, 4.8.1.) A port that keeps both halves is
implementing a documented branch of the original specification, not inventing
one.

### Recovery

`HttpApplication.ExecuteStep` (`HttpApplication.cs:2194`) is the authoritative
recovery point, and its shape exists because the CLR re-raises a
`ThreadAbortException` at the end of every catch block that does not
`ResetAbort`. An inner `catch (Exception e)` records the error; the automatic
re-raise at the end of that catch is what the outer frame observes:

```csharp
catch (ThreadAbortException e) {
    if (e.ExceptionState != null && e.ExceptionState is CancelModuleException) {
        CancelModuleException cancelException = (CancelModuleException)e.ExceptionState;
        if (cancelException.Timeout) {
            error = new HttpException(SR.GetString(SR.Request_timed_out), null,
                                      WebEventCodes.RuntimeErrorRequestAbort);
            PerfCounters.IncrementCounter(AppPerfCounter.REQUESTS_TIMED_OUT);
        }
        else {
            error = null;
            _stepManager.CompleteRequest();
        }
        Thread.ResetAbort();
    }
}
```

Everything downstream keys off those two lines. `error = null` is why
`Response.End` never reaches `RecordError` (`HttpApplication.cs:2305`), never
raises `Application_Error` (`RaiseOnError`, `HttpApplication.cs:616`), and
leaves `Server.GetLastError()` null. `_stepManager.CompleteRequest()`
(`HttpApplication.cs:3789`) sets `_requestCompleted`, which
`ApplicationStepManager.ResumeSteps` reads at `HttpApplication.cs:3922`:

```csharp
if (_currentStepIndex < _endRequestStepIndex && (context.Error != null || _requestCompleted)) {
    context.Response.FilterOutput();
    _currentStepIndex = _endRequestStepIndex;
}
```

So the skip set is decided by one index jump: every step between the aborting
step and `EndRequest` is skipped — remaining module events for the current
stage, `PostRequestHandlerExecute`, `ReleaseRequestState`,
`PostReleaseRequestState`, `UpdateRequestCache`, `PostUpdateRequestCache` — while
`FilterOutput` still runs, `EndRequest` runs in full, and
`HttpRuntime.FinishRequest` (`HttpRuntime.cs:1747`) flushes and calls
`EndOfRequest`.

Ten other sites recognize the same signal. The complete inventory, because
Reference Source names it by catching `ThreadAbortException`:
`HttpApplication.cs:2232` (a COM+ component re-throwing a caught abort),
`HttpApplication.cs:3453` (`OnAsyncHandlerCompletion`), `HttpContext.cs:1840`
(`InvokeCancellableCallback`, timeout flavour only), `UI/Page.cs:4876` and
`UI/Page.cs:5197` (the `ProcessRequestMain` fast path for a synchronous
redirect, guarded by four conditions), `UI/Page.cs:5820` (thread hygiene),
`UI/LegacyPageAsyncTask.cs:195`, and `Hosting/ISAPIRuntime.cs:180`. The
predicates differ from one another — some inspect `ExceptionState`, some unwrap
`InnerException`, one accepts any abort — which is itself a finding: there is no
shared `IsCancellationException` helper to reuse.

### Cancellable periods

`ExecuteStep` opens a cancellable period around cancellable steps only
(`HttpContext.BeginCancellablePeriod`, `HttpContext.cs:1748`;
`IsInCancellablePeriod` is `_timeoutState == 1`, `HttpContext.cs:1770`).
`SyncEventExecutionStep`, `CallFilterExecutionStep`, `SendResponseExecutionStep`
and a synchronous `CallHandlerExecutionStep` are cancellable; async event steps
and an async handler launch are not (`HttpApplication.cs:3632`, *"launching of
async handler should not be cancellable"*). `HttpServerUtility.ExecuteInternal`
explicitly closes the period around an async child (`httpserverutility.cs:524`).
`Response.End` therefore takes the non-cancellable arm on exactly the paths
where unwinding would be unsafe, and `_endRequiresObservation` /
`ObserveResponseEndCalled` (`HttpResponse.cs:3104`, called only from
`Util/WithinCancellableCallbackTaskAwaitable.cs:53`) converts a deferred `End`
into a real abort when an `await` resumes into a cancellable region. This latch
is the closest thing in the original to a cooperative checkpoint, and the port
should reuse it rather than invent one.

### Redirect

`Redirect(String)` → `Redirect(url, true, false)` (`HttpResponse.cs:2288`);
`RedirectPermanent(String)` → `Redirect(url, true, true)` (`2359`). Every
`RedirectToRoute*` overload hardcodes `endResponse: false` (`2330`) and so never
terminates. The core (`2372`) validates (null url, embedded newline, headers
already sent, page callback), transforms the url, calls `Clear()` — the entity
buffer only, not `ClearHeaders()` — sets `301`/`302` and `RedirectLocation`,
writes the three-line "Object moved" body (`2429`), sets
`_isRequestBeingRedirected`, raises the internal `Redirecting` event, and then
`if (endResponse) End();`.

`Redirect(url)` and `Redirect(url, false)` produce a byte-identical response.
The only difference is whether the caller keeps running — and a caller that
keeps running can append to that response, which is what makes the difference
observable. `RedirectToErrorPage` uses the non-ending form
(`HttpResponse.cs:2499`) because an abort during error reporting would be fatal.

### CompleteRequest

`HttpApplication.CompleteRequest()` (`HttpApplication.cs:536`) sets the same
`_requestCompleted` flag and returns. It does not throw and does not unwind: the
calling step runs to completion and only subsequent steps are skipped. That is
the whole difference from `End`, and it is why KB 312629 recommends
`Response.Redirect(url, false)` followed by
`Context.ApplicationInstance.CompleteRequest()` while warning that *"the code
that follows `Response.Redirect` is executed."* `HttpServerUtility.TransferRequest`
(`httpserverutility.cs:798`) makes the same substitution deliberately, with the
comment naming performance as the reason.

### executionTimeout

`RequestTimeoutManager` (`RequestTimeoutManager.cs:20`) scans on a **15-second**
`System.Threading.Timer`. `HttpContext.MustTimeout` (`HttpContext.cs:1774`)
refuses to abort when `debug="true"` or a debugger is attached, transitions
`_timeoutState` `1 → -1` under a CAS, and returns the request thread;
`TimeoutIfNeeded` (`RequestTimeoutManager.cs:177`) then calls
`thread.Abort(new HttpApplication.CancelModuleException(true))`. The
`-1` state exists for one race: `EndCancellablePeriod` CASes `1 → 0` so it
cannot clobber it, and `WaitForExceptionIfCancelled` (`HttpContext.cs:1765`)
spins until the abort arrives. `ThreadAbortOnTimeout` and `TimedOutToken`
(`HttpContext.cs:1655`) are the .NET 4.5-era cooperative opt-out already present
in the source.

Unlike `Response.End`, a timeout **is** an error: `ExecuteStep` produces
`HttpException(SR.Request_timed_out)` — *"Request timed out."*
(`System.Web.txt:276`) — with no HTTP code, so it renders as a 500.

### Server.Transfer and Server.Execute

`Transfer` is exactly `Execute` plus `Response.End()`
(`httpserverutility.cs:685`, `:730`), so it inherits termination wholesale.
`ExecuteInternal`'s synchronous child path (`httpserverutility.cs:616`) catches
into `error` with no abort special-case; on Framework the CLR's automatic
re-raise discards that assignment and the abort keeps unwinding past the
`HttpException` wrapper at `:664`, terminating the **outer** request. This is a
load-bearing accident of the auto-rethrow, and it is exactly where the prior
prototype broke (below).

### What a consumer's catch block sees

On Framework: a `ThreadAbortException` with `Message` *"Thread was being
aborted."* and `ExceptionState` set to the internal `CancelModuleException`.
Whether the application catches it by name, catches it as `Exception`, swallows
it, or re-throws it makes **no observable difference**, because the CLR re-raises
it at the end of the catch block regardless. The one escape is
`Thread.ResetAbort()`, which fully cancels the termination and resumes the page.

## Framework readings

Taken 2026-08-05 on `win-oracle`, `System.Web` 4.8.9319.0
(`NET481REL1LAST_25H2_B`), classic pipeline, `debug="false"`,
`executionTimeout="2"`, driven through `ApplicationManager.CreateObject` +
`SimpleWorkerRequest` — the same activation entry point the parity oracle uses.
Ad-hoc oracle readings, per ADR 0045; no golden session was created or touched.

A reflection probe first confirmed 4.8.1 still carries the pinned machinery
verbatim: `HttpResponse._completed`, `HttpResponse._endRequiresObservation`,
`HttpApplication+CancelModuleException` with base `System.Object` and a
`Timeout` property, `HttpContext.IsInCancellablePeriod`, `_timeoutState`,
`ThreadAbortOnTimeout`.

| # | Stimulus | Observation |
| --- | --- | --- |
| R1 | `Response.End()` in `Page_Load` | `ThreadAbortException`, `ExceptionState` = `HttpApplication+CancelModuleException` with `Timeout=False`, message *"Thread was being aborted."* |
| R2 | app catch swallows vs. re-throws the abort | Byte-identical response and identical stage trace. The CLR re-raise makes the two indistinguishable |
| R3 | after `End()` | inner `finally`, outer `finally`, `Page_Unload` all run; `PostRequestHandlerExecute`, `ReleaseRequestState`, `UpdateRequestCache` skipped; `EndRequest` runs; status 200; body is only what was written before `End()`; `Application_Error` does not fire; `Server.GetLastError()` null at `EndRequest` |
| R4 | app calls `Thread.ResetAbort()` in its catch | Termination fully cancelled: the rest of `Page_Load`, `Page_PreRender`, the render, and every skipped stage all run. Body is pre-`End` bytes plus the full page |
| R5 | `Response.Redirect("/target.aspx")` | Same abort; `302 Found`; `Location: /target.aspx`; body is exactly the "Object moved" HTML; same skip set; `EndRequest` runs; no `Application_Error` |
| R6 | `Response.Redirect("/target.aspx", false)` | No exception; following code runs; all stages run; status 302 with the same body, then the post-redirect write, then the full rendered page appended |
| R7 | write, then `Redirect(url)` | Earlier buffered content is discarded; body is the "Object moved" HTML alone |
| R8 | `Flush()`, then `Redirect(url)` | `HttpException` *"Cannot redirect after HTTP headers have been sent."*, `GetHttpCode()` 500, thrown to the caller; the page continues and renders; status stays 200 |
| R9 | `Flush()`, then `End()` | Still aborts (the period is still cancellable); bytes written after the flush are still sent |
| R10 | `CompleteRequest()` in `Page_Load` | No exception; following code runs; the page renders in full; `PostRequestHandlerExecute`, `ReleaseRequestState`, `UpdateRequestCache` skipped; `EndRequest` runs |
| R11 | `Response.End()` in `Application_BeginRequest` | Aborts; the module's `finally` runs; the handler never runs; only `EndRequest` follows; body is the module's bytes; status 200; no `Application_Error` |
| R12 | `CompleteRequest()` in `Application_BeginRequest` | Identical observable outcome to R11, minus the exception |
| R13 | `Server.Transfer("target.aspx")` | Aborts in the parent; body is the parent's pre-transfer bytes **plus** the child's full render — `Transfer` does not clear the parent buffer; same skip set |
| R14 | `Server.Execute("target.aspx")` | No abort; body is parent-pre, child, parent-post, parent render |
| R15 | `Server.Execute` of a child that calls `Response.End()` | The termination escapes the child and ends the **outer** request; body is parent-pre plus the child's bytes |
| R16 | `Response.End()` inside a `finally` block | Aborts normally |
| R17 | 45 s sleep under `executionTimeout="2"` | Abort delivered 11.4 s after entry; `ExceptionState.Timeout=True`; message *"Thread was being aborted."*; app `finally` blocks and `Page_Unload` run; `Application_Error` fires with `HttpException` / *"Request timed out."*; `Server.GetLastError()` non-null at `EndRequest`; final response is a 500 |
| R18 | 9 s sleep under `executionTimeout="2"` | Completed normally. The 15-second scan never observed it: enforcement latency is up to one scan period beyond the configured budget |

Readings still wanted, deferred with the timeout policy: `ThreadAbortOnTimeout =
false`; `debug="true"`; a timeout during an asynchronous continuation; a
`Response.End` from an `Async="true"` page continuation (the non-cancellable
arm, which none of the readings above exercised).

### .NET 10 readings

Taken locally on .NET 10; each is a hard constraint on the mechanism.

| # | Observation |
| --- | --- |
| N1 | `Thread.CurrentThread.Abort(object)` and `Thread.ResetAbort()` both throw `PlatformNotSupportedException: Thread abort is not supported on this platform.` |
| N2 | `ThreadAbortException` is `sealed` with **zero public constructors**, so it can be neither thrown nor derived from |
| N3 | `ThreadAbortException.ExceptionState` still exists but is hardcoded `=> null` (`dotnet/runtime`), so Framework's discriminating predicate is structurally unreproducible |
| N4 | `catch (ThreadAbortException)` still compiles, so application source keeps building |
| N5 | An instance can be synthesized with `RuntimeHelpers.GetUninitializedObject` and thrown; it is caught by name and `finally` runs — but `ExceptionState` is still null, and there is **no automatic re-raise**: `catch (ThreadAbortException) { }` swallows it and execution continues |

## What the port does today

`IsInCancellablePeriod` is true during synchronous handler execution, so
`Response.End()` and `Response.Redirect(url)` reach `AbortCurrentThread` and
throw `PlatformNotSupportedException`. `ExecuteStep`'s inner
`catch (Exception e)` records it as an ordinary error and the request renders a
500. Neither is covered by any test: a grep over `tests/` for `Response.End`,
`Response.Redirect`, `executionTimeout`, or `ThreadAbort` finds nothing but one
comment in `tests/parity/fixture/web.config`.

`CompleteRequest` works and is the one termination surface with standing
evidence: `tests/parity/src/Rehost.WebForms.Parity.Probes/ProbeModule.cs` calls
it on `BeginRequest` for `/complete`, and the committed Framework golden records
the skipped handler, the `EndRequest`, and a single `EndOfRequest`.

The timeout path is worse than unimplemented. `HttpRuntime.Init` constructs
`RequestTimeoutManager`, `HttpApplication.OnThreadEnterPrivate` registers every
context, and after `executionTimeout` (default 110 s) `TimeoutIfNeeded` calls
`thread.Abort(...)` on the `Timer` callback thread with no `try`/`catch`. That
is an unhandled `PlatformNotSupportedException` on a thread-pool thread, which
ends the process — taking every in-flight request and the single hosted
application with it. `MustTimeout` has already CASed `_timeoutState` to `-1` by
then, so a request that reaches `WaitForExceptionIfCancelled` would spin
forever. **This was reachable by any slow request and has been neutralized
ahead of the rest of the story (ledger P51)**, independently of what the
eventual timeout policy turns out to be.

## Consumer-visible behavior inventory

Each claim is tagged with where it is decided, which selects its rung under
[writing tests](../writing-tests.md). "Imported (untouched)" means Reference
Source decides it over substrate already exercised on both platforms — rung 0,
recorded measurement plus a compatibility-map entry. "Imported (edited)" means
this story changes the code that decides it, so rung 0 no longer applies and the
claim needs a standing test naming the seam. "Port seam" means hosting-layer
code.

| # | Claim | Decided by | Rung |
| --- | --- | --- | --- |
| C1 | Code after `Response.End()` in a page handler does not run | Imported (edited) | 2 |
| C2 | `finally` blocks between the `End()` call and the pipeline boundary run | Imported (edited) | 2 |
| C3 | Bytes written before `End()` are sent; bytes written after it are not | Imported (edited) | 2 |
| C4 | `PostRequestHandlerExecute`, `ReleaseRequestState`, `UpdateRequestCache` do not run | Imported (edited) | 2 |
| C5 | `EndRequest` runs, and `EndOfRequest` is raised exactly once | Imported (edited) + port seam | 2 |
| C6 | `Application_Error` does not fire and `Server.GetLastError()` is null | Imported (edited) | 2 |
| C7 | Status and headers set before `End()` survive to the wire | Imported (edited) | 2 |
| C8 | `Page_Unload` runs | Imported (edited) | 2 |
| C9 | `End()` in a module event skips the handler entirely | Imported (edited) | 2 |
| C10 | A second `End()` after termination changes nothing | Imported (edited) | 1 |
| C11 | `Redirect(url)` emits 302 + `Location` + the exact "Object moved" body, and terminates | Imported (edited) | 2 |
| C12 | `Redirect(url, false)` emits the same response, returns, and following writes append to it | Imported (untouched) | 0 |
| C13 | `Redirect` discards previously buffered content | Imported (untouched) | 0 |
| C14 | `RedirectToRoute*` never terminates | Imported (untouched) | 0 |
| C15 | `Redirect` after headers are sent throws `HttpException` naming the cause | Imported (untouched) | 0 |
| C16 | `RedirectPermanent` emits 301 | Imported (untouched) | 0 |
| C17 | `CompleteRequest()` does not unwind; following code runs | Imported (untouched) | 2 (already gated) |
| C18 | `CompleteRequest()` skips the same stages as `End`, and `EndRequest` runs | Imported (untouched) | 2 (already gated) |
| C19 | A request exceeding `executionTimeout` is terminated | Imported (edited) | 2, deferred |
| C20 | Timeout records `HttpException` *"Request timed out."*, fires `Application_Error`, renders 500 | Imported (edited) | 2, deferred |
| C21 | `finally` blocks run on timeout | Imported (edited) | 2, deferred |
| C22 | `ThreadAbortOnTimeout = false` suppresses termination | Imported (edited) | 1, deferred |
| C23 | `debug="true"` disables timeout termination | Imported (untouched) | 0, deferred |
| C24 | `Server.Transfer` renders the child, terminates the parent, and does not clear the parent buffer | Imported (edited) | 2, conditional |
| C25 | `Server.Execute` does not terminate | Imported (untouched) | 0, conditional |
| C26 | `End()` inside a `Server.Execute` child terminates the outer request | Imported (edited) | 2, conditional |
| C27 | `catch (ThreadAbortException)` in application code no longer observes termination | Port contract | 2, plus a map row |
| C28 | An application `catch (Exception)` that swallows termination cannot produce further output or un-complete the request | Port contract | 2 |

C10, C22 and C27–C28 have no Framework counterpart to measure; they are the
port's own contract and must be stated as such in the compatibility map.

## Candidate mechanisms

### Prior art: the Portable.System.Web prototype

Rank-5 design evidence only (`../Portable.System.Web`, commit `351b531`,
+132/−152 across 10 files). It was an experiment, explicitly not production
grade, and this repo already records one cautionary tale from it
([machine key and viewstate bootstrap](machine-key-and-viewstate-bootstrap.md)).
It is cited here as evidence, never as authority.

What it establishes, and is worth reusing:

- The change is mechanical and small. Making `CancelModuleException` derive from
  `Exception` and routing every former `ThreadAbortException` catch to it is a
  ~150-line delta; `HttpResponse.cs` needed a two-line diff.
- Its seam inventory is correct, because Reference Source named the seams by
  catching the abort: `ExecuteStep`, `OnAsyncHandlerCompletion`,
  `InvokeCancellableCallback`, `Page.ProcessRequestMain` ×2,
  `LegacyPageAsyncTask`, `ObserveResponseEndCalled`.
- `Thread.Interrupt` is not a substitute for `Thread.Abort` — useful negative
  evidence, below.

What it got wrong, which is the more valuable half:

- **A catchable exception with no compensation makes `End()` a no-op.** Its
  `End()` on the cancellable path only throws: it does not flush, does not set
  `_ended`, does not call `CompleteRequest()`. The moment anything between the
  call site and `ExecuteStep` has a `catch (Exception)`, the page keeps
  rendering and the post-`End` bytes ship. The prototype converted the nine
  files that mentioned `ThreadAbortException` by name and left the assembly's
  other ~659 `catch (Exception)` sites unaudited — sites Reference Source could
  leave alone precisely because the abort was uncatchable.
- Two of those sites are load-bearing. `Util/SynchronizationHelper.cs` captures
  it into `syncContext.Error`, so a `Response.End` finishes on the error path
  with a YSOD reading *"Thread was being aborted."* And
  `HttpServerUtility.ExecuteInternal` captures it and wraps it in
  `HttpException("Error executing child request…")`, so any `Server.Execute` or
  `Server.Transfer` onto a child that calls `Response.End` — the common Transfer
  target shape — renders a 500 instead of the child's output.
- `InvokeCancellableCallback` swallows the non-timeout case outright, so on
  `Async="true"` pages `Response.Redirect` in a continuation stops nothing.
- `Thread.Interrupt` for timeouts cannot preempt a CPU-bound loop — the case
  `executionTimeout` exists for — and latches: an interrupt raised for request A
  can fire inside an unrelated later request B on the same pooled thread and
  500 it. Its `_pendingTimeout` flag is set once and never cleared, so
  `WaitForExceptionIfCancelled` re-throws at every subsequent step boundary,
  firing `Application_Error` repeatedly for one timeout.
- Its test suite contains **zero** termination coverage; a `public void End() {}`
  stub would pass it unchanged. Its own README carries a nine-item "Testing
  Required" checklist with every box unchecked.

The transferable conclusion: the file-level map is reusable, the exception model
is not. A portable `End` cannot be a catchable exception that relies on nothing
catching it.

### M1 — Throw-only control-flow exception

Make `HttpApplication.CancelModuleException` derive from `Exception`, have
`AbortCurrentThread` throw it, and match it directly in `ExecuteStep`. This is
the prototype's design.

Trade-off: minimal edit, and every downstream rule (skip set, `EndRequest`,
error recording, `EndOfRequest`) is preserved for free because it all keys off
`error` and `_requestCompleted`. But C1 and C3 fail for any application or
library `catch (Exception)` on the stack, silently and unpredictably.
**Rejected on its own.**

### M2 — Synthesize a `ThreadAbortException`

`RuntimeHelpers.GetUninitializedObject` does produce a throwable instance that
`catch (ThreadAbortException)` matches (N5).

Trade-off: it satisfies C27 literally and nothing else. `ExceptionState` is
permanently null (N3), so the imported predicate can never match and the
termination escapes as an unhandled managed error. There is no automatic
re-raise (N5), so swallowing is *more* likely than under M1, not less. And it
reports a platform state that does not exist, which the "never surprise a future
consumer" rule forbids more strongly than an honest divergence. **Rejected.**

### M3 — Flag only, no unwind

The `dotnet/systemweb-adapters` shape: `IHttpResponseEndFeature.EndAsync()` sets
a state-machine flag, `HttpApplication.CompleteRequest()` delegates *to*
`Response.End()` (the inverse of Framework), and MVC compensates after the fact
with a `ResponseEndFilter` that discards the action result. Microsoft's adapters
accept that code after `Response.Redirect(url)` runs.

Trade-off: simple, and it has the `HttpResponse.End` remarks as documented
cover. But it abandons C1, the single most depended-on observable in the whole
story, for every application rather than only for those with a swallowing catch.
**Rejected as the primary mechanism**; it is the right behavior for the
non-cancellable arm, which Framework already implements this way.

### M4 — Cooperative cancellation

A `CancellationToken` checked at pipeline checkpoints. Trade-off: the only
portable option for timeouts, and Framework already ships the surface
(`HttpContext.TimedOutToken`, `ThreadAbortOnTimeout`). It cannot unwind
arbitrary synchronous user code, so it cannot implement `End`, and the prototype
demonstrates that adding `Thread.Interrupt` on top does not close that gap.

### M5 — Recommended: latch plus control-flow exception

Assemble the recovered intent from the two arms Framework already ships. On the
cancellable path, `End()` does the non-cancellable arm's work **and then**
throws:

1. set `_ended` and call `ApplicationInstance.CompleteRequest()` — Framework's
   own documented non-aborting behavior, which makes the request completed and
   further output discarded *as state*, not as control flow;
2. throw the port-owned control-flow exception to unwind, preserving C1 for the
   overwhelming majority of call sites;
3. re-throw at the next `ExecuteStep` boundary if the latch is set and the
   request is already completed — the port's affordable approximation of the
   CLR's automatic re-raise, reusing the existing `_endRequiresObservation` /
   `ObserveResponseEndCalled` seam rather than inventing one.

The exception type is the imported `HttpApplication.CancelModuleException`,
promoted to derive from `System.Exception` and kept `internal`. Reusing it keeps
the imported predicates nearly unchanged, adds no public API (small public API,
deep internal implementation), and is honest: applications could never name it
on Framework either, because what they caught was the `ThreadAbortException`
wrapping it. It must **not** derive from `HttpException` (which
`ExecuteInternal` inspects by HTTP code) or from `OperationCanceledException`
(which ASP.NET Core middleware treats as a benign client abort).

`Message` should be resourced and should describe what actually happened, not
imitate *"Thread was being aborted."* A log line that claims a thread abort on a
platform without thread aborts is the kind of surprise the compatibility policy
exists to prevent. The prototype hardcoded the Framework string; do not.

#### The residual gap, stated plainly

An application that writes `catch (ThreadAbortException)` compiles (N4) and the
block never executes. An application that writes `catch (Exception)` and
swallows sees termination reach its handler, and unlike Framework it can prevent
the unwind from continuing. What it cannot do, under M5, is produce further
output (`_ended` discards it) or un-complete the request (`_requestCompleted` is
already set), and the next step boundary re-throws. So the divergence narrows
to: *statements between a swallowing catch and the end of the current pipeline
step still execute, and their side effects outside the response are visible.*
That is the documented residual gap. It is not zero, and it must appear in the
compatibility map as a named boundary rather than as "supported".

`Thread.ResetAbort()` (R4) has no counterpart and is not offered; an application
that deliberately cancels termination is broken by this port, explicitly.

#### Imported-source edits and ledger implications

`HttpResponse.End`'s Framework path cannot run unmodified — `Thread.Abort`
throws at the first call. The edit set is narrow and each item is forced:

| File | Edit |
| --- | --- |
| `HttpResponse.cs` | `AbortCurrentThread` throws instead of aborting; `End()`'s cancellable arm also sets `_ended` and calls `CompleteRequest()` |
| `HttpApplication.cs` | `CancelModuleException : Exception`; `ExecuteStep` matches it directly and drops `Thread.ResetAbort()` and the unreachable COM+ clause; `OnAsyncHandlerCompletion` keeps its unwrapping predicate |
| `HttpContext.cs` | `InvokeCancellableCallback` must re-throw the non-timeout case rather than swallow it (the prototype's async defect); `WaitForExceptionIfCancelled` must not spin |
| `RequestTimeoutManager.cs` | already neutralized (ledger P51) |
| `UI/Page.cs` | three catch sites plus `ThreadResetAbortWithAssert`; keep the four-condition fast-path guard intact |
| `UI/LegacyPageAsyncTask.cs` | one catch site |
| `Hosting/IPipelineRuntime.cs`, `Configuration/ConfigUtil.cs` | keep `ThreadAbortException` in the fatal/rethrow predicate lists **and add** `CancelModuleException` where the intent was "must propagate" — the prototype dropped the guard with nothing in its place |

Beyond the named sites, the edit converts an uncatchable signal into a catchable
one across the whole assembly. That blast radius — every `catch (Exception)` in
imported source — is what M5's latch exists to bound, and it is why the
`SynchronizationHelper` and `ExecuteInternal` sites need explicit review rather
than a grep for `ThreadAbortException`.

The `RequestTimeoutManager` neutralization landed ahead of this story as ledger
P51. One further row (next free number) covers the rest of thread-abort removal:
the sites, the mechanism, the residual gap, and the removal of `SYSLIB0006` from
the runtime project's `NoWarn`, which is already listed in the parent follow-up's
acceptance criteria. `Hosting/ISAPIRuntime.cs` is unreachable and needs no edit;
note it in the row rather than touching it.

## Failure classification

Against [ADR 0038](../adr/0038-classify-failures-by-ownership-and-mutation.md)
and the terminology in `CONTEXT.md`:

- **`Response.End`, terminating `Redirect`, and `Transfer` are not failures at
  all.** They produce no `context.Error`, System.Web completes normally, and the
  adapter observes an ordinary `EndOfRequest`. They belong to no bucket. Saying
  so explicitly matters, because a mechanism that surfaces them as exceptions
  invites the hosting layer to classify them as **adapter failures**.
- **A timeout is a managed request failure.** System.Web owns and formats it
  (`ReportRuntimeError` → 500), `Application_Error` fires, `EndOfRequest` still
  happens, and only the response body reports it. This is the existing bucket
  with no adjustment.
- **A termination exception that escapes `HttpRuntime.ProcessRequest` is an
  adapter failure** by ADR 0014 and ADR 0016: the middleware's
  `FailCompletion` faults `workerRequest.Completion` and the exception is
  observed exactly once. This is a mechanism bug, not a supported path, and the
  story owes a test that it cannot happen.

No ADR amendment is required. ADR 0038 classifies failures; termination is not
one, and the two termination cases that *are* failures already fit. Recommend
recording the classification in this document and in the compatibility map
rather than amending an accepted ADR to say that something is out of its scope.

## Test plan

Per [writing tests](../writing-tests.md); walk down and stop at the first rung
that fits.

**Rung 0 — no standing test.** C12–C16, C23, C25. These are decided by
`HttpResponse.Redirect`'s and `HttpServerUtility.Execute`'s untouched bodies
over substrate the page and postback scenarios already exercise on both
platforms, with no first reach of a platform-sensitive leaf. Evidence is the
recorded readings above (R6, R7, R14, and the `RedirectToRoute` hardcoding at
`HttpResponse.cs:2330`) plus compatibility-map rows. Note that C11 does *not*
qualify: `Redirect(url)` ends in `End()`, which this story edits.

**Rung 1 — plain unit test.** C10 and C22. These are property-level claims about
the latch and the opt-out with no host in the loop. Files mirror the source they
cover: imported types at the root of `src/System.Web.ReferenceSource` map to the
root of `tests/Rehost.WebForms.Runtime.Tests/`.

**Rung 2 — scenario over the shared host.** Everything else. Join
`Fixtures.Page` (`tests/Rehost.WebForms.ScenarioHost/fixtures/page`), which
already has a `Global.asax` and a witness endpoint. Add probe pages —
`End.aspx`, `Redirect.aspx`, `Transfer.aspx`, `Execute.aspx`, and a
swallowing-catch variant — and extend the fixture's application events to record
stage names to the witness, so C4, C5, C9 and C18 assert on an ordered stage
list rather than on rendered text. Act in the test, assert on the typed
response first and on `scenario.Witness` for server-side facts; nothing new may
poll the file journal, and raw trace strings never appear in tests.

A **new fixture** is warranted for the timeout claims and only those: they need
`<httpRuntime executionTimeout="…">`, which is conflicting configuration — the
structural reason ADR 0045 requires — and it must be recorded in the fixtures
README. Deferred with the rest of the timeout policy.

**Rung 3 — differential.** None. Under ADR 0044 neither trigger holds: the port
is not replacing native or host-owned code here, and it is not matching a format
it cannot derive from its own inputs — the 302, the `Location` header, and the
"Object moved" body all come from imported source it did not write. The Framework
evidence this story needs is the ad-hoc oracle readings recorded above, which is
what ADR 0045 prefers; **no new golden session**, and no new step against the
committed golden. If the stage-skip set later needs pinning against Framework,
propose it as a captured fixture in the `Framework.postback` pattern, not as a
session.

**Mutation verification.** Each claim must fail by name when the mechanism
degrades, and the failure must be an assertion, not a hang:

| Degradation | Test that fails, and how |
| --- | --- |
| `End()` throws nothing (pure flag, M3) | the C1 test fails on the response body containing the post-`End` marker |
| `End()` throws but does not set `_ended`/`CompleteRequest` (M1) | the C28 test fails: the swallowing page's later output appears |
| No re-throw at the step boundary | the C28 test fails on the stage list showing `PostRequestHandlerExecute` |
| Termination classified as an error | the C6 test fails on the witness containing `application-error`, and the status assertion flips 200 → 500 |
| `CompleteRequest()` not called | the C4 test fails because the skipped stages appear in the witness list |
| Timeout never fires | the C19 test fails on a bounded client timeout, asserted as an elapsed-time bound rather than left to hang |

A test asserting only "status 200" would pass against a stub; every claim asserts
a discriminating literal, and the literal stays in the test.

## Open decisions

All resolved — see [Decisions](#decisions-2026-08-05). The analysis is kept
as recorded at review time.

1. **Exception type and derivation.** Recommend M5's promoted, `internal`
   `HttpApplication.CancelModuleException : Exception` with a resourced message
   that does not imitate *"Thread was being aborted."* The alternative — a new
   public `Rehost`-namespaced type applications could catch by name — is
   rejected: it grows the public surface, has no Framework counterpart, and
   invites application code that will not compile against Framework.
2. **`executionTimeout` in or out of scope.** Recommend **out**, split into its
   own story, with one exception: the `RequestTimeoutManager` `thread.Abort`
   call must be neutralized in this story or ahead of it, because it ends the
   process today. Termination via `End` is a control-flow change decided in two
   files; enforcing a timeout against arbitrary synchronous user code on a
   platform that cannot preempt it is a product-policy question — cooperative
   only, connection abort, or explicit rejection under the portability contract
   — and the prototype shows that answering it casually produces worse behavior
   than not answering it.
3. **`Server.Transfer` scoping.** Recommend claiming C24 and C26 only if
   `Server.Execute` is otherwise supported by the time this lands; transfer and
   execute are their own bullet in
   [deferred request surfaces](deferred-request-surfaces.md). Once `End` works,
   `Transfer`'s termination works for free — but R15 and the prototype's
   `ExecuteInternal` defect show the child-terminates-parent path is a real
   trap, so if Transfer is out of scope it must be recorded as unassessed in the
   map, not left silent.
4. **How many re-throw checkpoints to buy.** Recommend the next `ExecuteStep`
   boundary only, with output discarded in between. Instrumenting
   `HttpResponse.Write` and `Flush` would narrow the gap further at the cost of
   touching the hottest path in the runtime; the gap it closes is side effects
   outside the response, which no checkpoint can prevent anyway.
5. **ADR amendment.** Recommend none; record the classification here.

## Decisions (2026-08-05)

Resolved in review, 2026-08-05. The three highest-stakes claims were
spot-verified first: the `thread.Abort` at `RequestTimeoutManager.cs:177` and
its uncaught path from the 15-second `Timer` (`HttpRuntime.cs:308`,
`HttpApplication.cs:2122`, `RequestTimeoutManager.cs:43`); the two-arm
`HttpResponse.End` body and the 4.8.1 API remarks quoted above, refetched
verbatim; and N1–N5 re-run locally on .NET 10 (`sealed`, zero public
constructors, `ExceptionState` null on a synthesized instance, no automatic
re-raise).

1. **Defect fix timing.** Neutralize the `RequestTimeoutManager` process-killer
   now, as a standalone surgical commit ahead of the rest of this story: its own
   portability-ledger row and a focused test that a slow request no longer ends
   the process. The neutralization must prevent the `MustTimeout` state flip,
   not merely catch the throw, or the request trades a crash for an infinite
   spin in `WaitForExceptionIfCancelled`. Rationale: reachable today by any
   request exceeding `executionTimeout` while this story waits behind the
   cookies story, and independent of the eventual timeout policy — which stays
   out of scope as its own story (open decision 2 as recommended).

2. **Mechanism.** M5 adopted as specified, including the promoted `internal`
   `CancelModuleException : Exception` with a resourced, non-imitating message
   (open decision 1 as recommended) and no `ResetAbort` counterpart. Rationale:
   completing the request as state before throwing bounds the blast radius of a
   catchable signal — a swallowing catch can no longer produce output or
   un-complete the request, the prototype's central defect — and both halves
   are behavior Framework shipped and documented.

3. **Swallowing-catch fidelity.** Re-throw at the next `ExecuteStep` boundary
   only; no guards in `Response.Write`/`Flush` (open decision 4 as
   recommended). The residual gap — statements between a swallowing catch and
   the end of the current step run, and their side effects outside the response
   are visible — enters the compatibility map as a named boundary. Rationale:
   write guards shrink the gap unpredictably without closing it (side effects
   without writes are exactly what they miss) while taxing the hottest path for
   every request. They remain possible later behind the same latch if a ported
   application hits the gap.

4. **`Server.Transfer`/`Execute`.** Recorded as unassessed in the compatibility
   map until a Server.Execute story exists; C24–C26 are not claimed by this
   story. R13–R15 and the `ExecuteInternal` swallowing-catch trap stay recorded
   here for that story. Rationale: claiming requires the child-execution
   surface end-to-end — a scope expansion — and a claim without tests is the
   silent partial support the project forbids.

No ADR amendment (open decision 5 as recommended).

## Sequencing

This implements **after** the in-flight cookies story. That story does not yet
exist as a document — cookies are currently an unowned bullet in
[deferred request surfaces](deferred-request-surfaces.md) — so the dependency is
on the work, not on a file. Both stories touch the adapter response path.

Files both are expected to touch:

- `src/System.Web.ReferenceSource/HttpResponse.cs` — cookies in the header
  generation region, termination in `End`/`Redirect`/`AbortCurrentThread`
  (roughly lines 2288–2447 and 3085–3116). Same file, disjoint regions; textual
  conflict risk is low.
- `src/Rehost.WebForms.Hosting/ResponseSpool.cs` — the real collision. Cookies
  will likely add typed cookie state beside the header list; termination needs a
  discard that clears **all** buffered response state, so a cookie list added
  after this story's discard is written will be missed by it. Rebase this
  story's discard onto the final spool shape, not before it.
- `src/Rehost.WebForms.Hosting/RehostWebFormsMiddleware.cs` — `CommitAsync`
  header append and status assignment.
- `src/Rehost.WebForms.Hosting/AspNetCoreWorkerRequest.cs` —
  `SendKnownResponseHeader`, `SendStatus`, `EndOfRequest`, `Seal`.
- `tests/Rehost.WebForms.ScenarioHost/fixtures/page/` and
  `tests/Rehost.WebForms.Hosting.Tests/Fixtures.cs` — both add fixture pages and
  probes to the shared `page` fixture.
- `docs/follow-ups/compatibility-feature-map.md` — both owe rows; ADR 0045's
  rule is that story completion updates the map.

## Done when

- `Response.End` and `Response.Redirect(url)` terminate a request over Kestrel
  on macOS `arm64` and Windows `x64`, and C1–C11 hold with tests that fail by
  name when the mechanism is degraded.
- The residual gap for `catch (ThreadAbortException)` and for a swallowing
  `catch (Exception)` is a named boundary in the compatibility feature map, not
  an unstated divergence.
- No reachable call to `Thread.Abort` or `Thread.ResetAbort` remains, and
  `SYSLIB0006` is removed from the runtime project's `NoWarn`.
- A slow request can no longer end the process from the timeout timer thread.
- One portability-ledger row records the thread-abort removal, its residual gap,
  and the sites deliberately left untouched.
- The timeout policy is either delivered or is an owned story with acceptance
  criteria, and `Server.Transfer`/`Execute` are either claimed or recorded as
  unassessed.
