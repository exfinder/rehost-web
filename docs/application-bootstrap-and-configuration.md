# Application bootstrap and configuration

## Host contract

The host creates one process-scoped `WebFormsApplication` from immutable
options. Registration does not load application code, create a
`HostingEnvironment`, or mutate legacy global state. The first routed request
single-flights activation.

Required options:

- opaque, non-empty application ID;
- absolute existing physical application root;
- absolute virtual root (`/` or an application subpath);
- absolute writable application work root.

The physical root is normalized with `Path.GetFullPath`, retains filesystem
casing, and is stored with a trailing platform directory separator. The
virtual root rejects relative paths, backslashes, query/fragment text, and
literal traversal segments.

Registration does not resolve symlinks or establish descendant containment;
[portable path mapping](follow-ups/portable-path-mapping-and-containment.md)
owns that boundary.

Host registration validates without global mutation and may be retried after a
validation failure. Once activation begins mutating legacy global state, only
one attempt is allowed. An escaping activation failure is terminal for that
generation; failures owned by the managed request pipeline retain their
Framework scope and error processing.

## Configuration sources

Build and package consumers receive versioned portable baselines under the
host output directory:

```text
configs/rehost-webforms.machine.config
configs/rehost-webforms.web.config
```

Packaging publishes versioned baseline assets deterministically. The owning
host passes their absolute paths during registration; runtime code does not
discover them from `AppContext.BaseDirectory` or Runtime assembly location.

The application configuration source is optional
`<physical-root>/web.config`. Missing application configuration means baseline
inheritance only. Arbitrary external application configuration paths are not
supported in this iteration.

The machine baseline adapts the pinned System.Web configuration vocabulary.
Root-web defaults are derived structurally from pinned .NET Framework 4.8.1
configuration. Every assembly-identity or portability delta is inventoried.
First-slice fixtures clear inherited handlers and modules before registering
their probe components. The earlier Portable.System.Web POC is not a source.

## Validation and commit

Before global mutation, registration:

1. validates and opens each required file;
2. validates application identity and physical, virtual, and work roots; and
3. verifies no conflicting process-wide application binding exists.

Registration does not open mapped System.Web configuration or eagerly resolve
application sections. The retained `HostingEnvironment` and
`HttpRuntime.HostingInit` sequence owns configuration installation, parsing,
inheritance, caching, and diagnostics. Portable policy checks occur where that
sequence normally consumes the relevant section.

During activation, the immutable binding is mirrored into current-AppDomain
data slots required by imported code. These values are explicit owner output,
not IIS/ambient identity. Binding failure restores prior slots where reliable;
an escape after global mutation requests terminal host shutdown. The
host-neutral current-AppDomain leaf rejects conflicting roots, IIS Express,
native configuration tokens, and reload.

## Portability and lifecycle

- One application per process/current AppDomain.
- No IIS, registry, runtime-assembly-location, or secondary-AppDomain lookup.
- Configuration is immutable for process lifetime; changes require restart.
- Runtime always uses full trust.
- `FEATURE_PAL` is not defined by the Runtime project. Narrow portable
  deviations retain Framework behavior under `NETFRAMEWORK`.

Configuration reload/restart design:
[configuration reload](follow-ups/configuration-reload-and-process-restart.md).

## Integration verification

- ASP.NET Core uses the process-scoped application owner; middleware never
  initializes `HostingEnvironment` directly.
- Request startup must prove activation and the first request reach no native
  IIS/Windows operations.
- Runtime codegen later verifies
  `PreApplicationStartMethodAttribute` and `App_Code.AppInitialize` order.
- Dynamic startup later verifies classic `Global.asax.Application_Start`
  request-pipeline behavior.

Owning stories:
[host adapter](follow-ups/aspnet-core-host-adapter.md),
[request startup](follow-ups/request-startup-portability.md),
[runtime codegen](follow-ups/runtime-codegen-and-loading.md), and
[dynamic integration](follow-ups/dynamic-aspx-integration.md).
