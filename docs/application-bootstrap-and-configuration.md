# Application bootstrap and configuration

This document describes the current implementation. The intended owner-based
lifecycle remains in [ADR 0002](adr/0002-application-lifecycle.md).

## Host contract

`AddRehostWebForms` builds `WebFormsApplicationOptions` and calls the static
`WebFormsApplication.Initialize` synchronously. Initialization must complete
before the host listens and may be called exactly once per process.

Required options:

- opaque, non-empty application ID;
- absolute existing physical application root;
- absolute virtual root (`/` or an application subpath).

Machine and root-web configuration paths are optional. When omitted, they
default to the `configs` directory under `AppContext.BaseDirectory`.
`CompilationTempDirectory` optionally supplies the writable root for generated
output; the `REHOST_WEBFORMS_COMPILATION_TEMPDIRECTORY` environment variable
supplies the same root from the deployment. Any two of the option, the
variable, and `<compilation tempDirectory>` that disagree fail preflight.
Absent all three, the root is `codegen` under `AppContext.BaseDirectory`
(ADR 0008).

The physical root is normalized with `Path.GetFullPath`, retains filesystem
casing, and receives a trailing platform directory separator. The virtual root
rejects relative paths, backslashes, query/fragment text, and literal traversal
segments. The compilation temp directory is normalized without a trailing
separator and must not name a file.

Bootstrap does not resolve symlinks or establish descendant containment; the
adapter does, per [filesystem semantics](filesystem-semantics.md) (ledger P72).

## One-shot state and publication

The bootstrap state is:

`Uninitialized -> Initializing -> Initialized | Faulted`

Initialization:

1. acquires the single initialization attempt;
2. normalizes options and configuration paths;
3. loads the portable IIS baseline;
4. opens mapped System.Web configuration and validates required files,
   sections, target framework, trust, reload policy, and codegen storage;
5. validates then writes the legacy current-AppDomain application slots;
6. publishes the IIS configuration, immutable bootstrap configuration, and
   `Initialized` state.

A failure publishes `Faulted`; retry is unavailable. AppDomain-slot writes are
rolled back when the binding operation itself fails. A later failure may leave
global state mutated, so process replacement is required.

## Configuration sources

The host output carries:

```text
configs/rehost-webforms.machine.config
configs/rehost-webforms.web.config
configs/rehost-webforms.applicationHost.config
```

The first two paths may be overridden explicitly. The IIS baseline is resolved
beside the selected machine configuration file. The optional application file
is `<physical-root>/web.config`; missing means baseline inheritance only.

The machine and root-web baselines adapt pinned .NET Framework 4.8.1
configuration and mirror the golden files' shape: the same elements and
attributes, and each collection in Framework's own order, so an entry's position
is the position it holds in
[the golden reference](framework-config-reference.md). Conformance is the
default state of these files and needs no annotation; a *deviation* is what
carries a reason, in the file where it is not self-evident — `<browserCaps>`
substituting `HttpCapabilitiesBase` for the absent `MobileCapabilities` is the
worked example. Assembly-identity and portability deltas are inventoried in
[dependency decisions](dependency-decisions.md) and the
[portability ledger](portability-ledger.md).

## Activation and shutdown

Bootstrap is separate from managed-pipeline activation. `UseRehostWebForms`
registers host-stop cleanup. The first routed request evaluates a thread-safe
`Lazy<ClassicPipelineDispatcher>` and calls `ApplicationManager.CreateObject`.
That retained path creates the current-AppDomain `HostingEnvironment`, installs
mapped configuration, initializes `HttpRuntime`, and creates the registered
dispatcher. The same request then enters `HttpRuntime.ProcessRequest`.

Host shutdown stops the registered dispatcher, requests application shutdown,
and closes `ApplicationManager`. No runtime-originated host-stop notification
is currently wired.

## Portability boundary

- One application per process/current AppDomain.
- Configuration is immutable; changes require process replacement.
- No IIS, registry, secondary-AppDomain, or native configuration-token lookup.
- Runtime always uses full trust.
- `FEATURE_PAL` is not defined by the Runtime project. Narrow portable
  deviations retain Framework behavior under `NETFRAMEWORK`.

Open lifecycle work is indexed in the [backlog](backlog.md).
