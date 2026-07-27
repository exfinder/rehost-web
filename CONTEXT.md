# Terminology

Terms shared across architecture, contracts, and work packets:

- **Managed Web Forms runtime** — portable application startup, managed request
  processing, dynamic compilation, and completion. Avoid “core.”
- **Classic managed pipeline** — supported request path where System.Web owns
  the lifecycle through `HttpRuntime.ProcessRequest(HttpWorkerRequest)`.
- **IIS-integrated pipeline** — excluded IIS-owned profile driven by native
  notifications and module inventory.
- **Semantic oracle** — executable .NET Framework 4.8.1 reference used to settle
  uncertain observable behavior.
- **Core differential probe** — equivalent recording-worker-request fixture run
  against the oracle and portable runtime, independent of transport.
- **Adapter integration probe** — Kestrel test of translation, completion,
  disconnect, and host lifecycle; not evidence of internal System.Web parity.
- **Application runtime** — process-scoped owner of validation, activation,
  request admission, and shutdown for one Web Forms application.
- **Host registration** — mutation-free creation of an application runtime from
  explicit host options.
- **Application activation** — once-only transition from validated host state to
  an accepting runtime; normally triggered by the first routed request.
- **First-request initialization** — distinct once-only, request-dependent
  System.Web work performed with the first real request.
- **Compatibility envelope** — behaviors within which observed Framework
  semantics are preserved; everything else is a documented deviation or
  explicit rejection.
- **Legacy hosting adapter** — retained Framework hosting machinery that
  preserves sequencing without owning process lifecycle.
- **Host adapter** — host-specific request translator and completion/lifecycle
  relay; it does not own System.Web activation or object lifetimes.
- **Portability ledger** — reached-edge evidence: required postcondition,
  platform dependency, treatment, and proof.
- **Managed request failure** — application/module/handler failure owned and
  formatted by System.Web.
- **Activation escape** — exception leaving activation after legacy global
  mutation; terminal for the application generation.
- **Adapter failure** — translation, transport, or pre-pipeline failure owned by
  the host adapter.
- **Application generation** — immutable configuration, binaries, content, and
  generated state served by one application process.
- **Terminal shutdown notification** — one-shot request for the owning host to
  stop the generation; it neither restarts nor terminates the process.
