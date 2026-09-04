# Integrated-pipeline identity

`HttpRuntime.UsingIntegratedPipeline` and `HttpRuntime.IISVersion` are how a
library asks which pipeline it is running under. Both are written by
`PopulateIISVersionInformation` from a native call the port never makes, so they
answered `false` and `null` while everything else about the runtime models an
integrated application pool: the merged `<modules>` list (ledger P83), the
merged `<handlers>` list (P85) and IIS request filtering (P86) all come from the
shipped `applicationHost.config` baseline of an IIS 10 integrated pool.

Measured on winbox (IIS 10.0 / Windows 11, .NET Framework 4.8.9344, 2026-08-30):
one application served by two sites on the same server reports
`UsingIntegratedPipeline = True` and `IISVersion = 10.0` from the integrated
pool, and `False` / `8.0` from the classic pool — reading IV16 of the
[divergence audit](../research/integrated-divergence-audit.md). The classic
answer is a downlevel IIS version, not the server's.

## Decision

The public identity is an integrated IIS 10 pool: `UsingIntegratedPipeline`
returns `true`, `IISVersion` returns `10.0`. The internal
`HttpRuntime.UseIntegratedPipeline` stays `false`, and every site reading it
keeps its classic arm.

The split is deliberate, not an oversight. The internal flag selects the native
notification plumbing IIS supplies through `IIS7WorkerRequest` — the port's
engine is `HttpRuntime.ProcessRequest(HttpWorkerRequest)` ([ADR
0001](0001-runtime-compatibility-model.md)) and has none of it. The public
property is the feature-detection contract the migrating audience observed;
answering `false` there sends third-party code down classic branches that the
audience's own server never took. `Util/AppVerifier.cs` was the one imported
reader of the public property; it moves to the internal flag, because the
`NotificationContext` its assert demands exists only where that plumbing does.

`10.0` is the version IV16 measured on the server whose `applicationHost.config`
the port ships, so the two halves of the identity name the same pool.

## Consequences

- Callers now take their integrated branches. Every reader of either property in
  this repo is the OWIN host or the runtime itself (verified across every
  assembly under `apps/`, `third_party/`, `src/` and the package cache):
  `ShutdownDetector` subscribes to `HostingEnvironment.StopListening` and
  `OwinAppContext` probes the `WEBSOCKET_VERSION` server variable, neither of
  which the port answered when this ADR landed. Job 5 supplied both (ledger
  P92), so OWIN's shutdown token now cancels at the host stop and the
  advertised WebSocket capability is no longer withdrawn on the first request.
  `OwinCallContext.DisableResponseCompression` needed a narrow edit, recorded
  in [the Katana provenance](../provenance/aspnet-katana.md); the one
  `DisconnectWatcher` carried is gone with the token answering.
- Members that still refuse with "This operation requires IIS integrated
  pipeline mode" sit next to a `true` answer until job 6 rewords them:
  `HostingEnvironment.MaxConcurrentRequestsPerCPU` and
  `MaxConcurrentThreadsPerCPU`. A recorded refusal beside `true` is the
  accepted cost; a silent classic branch is not,
  which is the whole reason the identity flips first.
- Job 3 has landed: the `MapRequestHandler`/`LogRequest`/`PostLogRequest`
  subscriptions and the `HttpContext.RemapHandler` window now answer as the
  integrated pool a caller detects here (ledger P90), so the first family a
  detector reaches after the identity flip no longer refuses.
- Job 4 has landed the shim batch beside it (ledger P91):
  `HttpContext.CurrentNotification`/`IsPostNotification`, writable
  `Request.Headers`, `HttpApplication.OnExecuteRequestStep` and
  `Response.AddOnSendingHeaders` answer instead of refusing, the pre-send
  events carry `HttpContext.Current`, and a late `+=` is refused as integrated
  refuses it. `OwinCallContext.RegisterForOnSendingHeaders` reaches the same
  member reflectively; its registration is no longer refused, though no
  scenario exercises Katana's own flush notification yet.
- Job 5 has landed the remainder's build half (ledger P92):
  `Response.ClientDisconnectedToken`, `Request.Abort`, `Response.SubStatusCode`
  and `Request.InsertEntityBody` answer, the intrinsics are hidden during
  application and module init, the error page and its status land before
  `LogRequest`, and the two Katana gaps above are closed.
- `PopulateIISVersionInformation`, `_iisVersion` and `_useIntegratedPipeline`
  are untouched, so a Framework build of the imported tree is unaffected.
