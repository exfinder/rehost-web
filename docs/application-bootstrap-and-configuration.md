# Application bootstrap and configuration

## Host contract

The host calls `Rehost.WebForms.Hosting.WebFormsApplication.Initialize` exactly
once, before loading application code, creating a `HostingEnvironment`, or
dispatching requests.

Required options:

- opaque, non-empty application ID;
- absolute existing physical application root;
- absolute virtual root (`/` or an application subpath).

The physical root is normalized with `Path.GetFullPath`, retains filesystem
casing, and is stored with a trailing platform directory separator. The
virtual root rejects relative paths, backslashes, query/fragment text, and
literal traversal segments.

Bootstrap does not resolve symlinks or establish descendant containment;
[portable path mapping](follow-ups/portable-path-mapping-and-containment.md)
owns that boundary.

Initialization has four process-wide states: uninitialized, initializing,
initialized, and faulted. The first caller owns the only attempt. Concurrent
or later calls fail immediately, including calls with identical options. Any
failure is terminal until process replacement.

## Configuration sources

Build and package consumers receive versioned portable baselines under the
host output directory:

```text
configs/rehost-webforms.machine.config
configs/rehost-webforms.web.config
```

MSBuild copies newer baseline inputs with `PreserveNewest`. Defaults resolve
from `AppContext.BaseDirectory/configs`, never the Runtime assembly location.
Hosts may override either baseline with an absolute file path.

The application configuration source is optional
`<physical-root>/web.config`. Missing application configuration means baseline
inheritance only. Arbitrary external application configuration paths are not
supported in this iteration.

The machine baseline declares the portable System.Web configuration
vocabulary. The root-web baseline selects full trust, disables configuration
file change notifications, and otherwise remains minimal. Pipeline
handler/module defaults belong to the minimal-pipeline profile.

These baselines are independently authored from pinned Reference Source
configuration types and verified against Framework configuration hierarchy
semantics. The earlier Portable.System.Web POC is not a source.

## Validation and commit

Before global mutation, bootstrap:

1. validates and opens each required file;
2. uses `WebConfigurationManager.OpenMappedWebConfiguration` to load the
   proposed machine → root-web → application hierarchy;
3. resolves bootstrap-critical `httpRuntime`, `trust`, `compilation`, and
   `hostingEnvironment` sections;
4. rejects reload, partial trust, and legacy CAS.

`System.Configuration` owns XML, inheritance, `configSource`, and line/column
diagnostics. Custom/unrelated sections retain lazy Framework evaluation.
Bootstrap adds resolved source paths to configuration errors.

After preflight, the immutable binding is mirrored into current-AppDomain data
slots required by imported code. These values are explicit bootstrap output,
not IIS/ambient identity. Binding failure restores every prior slot before the
process becomes faulted. The host-neutral current-AppDomain overlay rejects
conflicting host roots, IIS Express, native configuration tokens, and reload.

## Portability and lifecycle

- One application per process/current AppDomain.
- No IIS, registry, runtime-assembly-location, or secondary-AppDomain lookup.
- Configuration is immutable for process lifetime; changes require restart.
- Runtime always uses full trust.
- `FEATURE_PAL` is not defined by the Runtime project. Narrow portable
  deviations retain Framework behavior under `NETFRAMEWORK`.

Configuration reload/restart design:
[configuration reload](follow-ups/configuration-reload-and-process-restart.md).

## Deferred integration verification

Bootstrap intentionally does not run application code.

- ASP.NET Core host adapter must call `Initialize` before all System.Web use.
- Request startup work must prove remaining hosting initialization reaches no
  native IIS/Windows operations.
- Runtime codegen integration must run and order
  `PreApplicationStartMethodAttribute` and `App_Code` `AppInitialize` before
  accepting a request.
- Dynamic ASPX integration must verify `Global.asax` `Application_Start`
  remains request-pipeline behavior.

Owning stories:
[host adapter](follow-ups/aspnet-core-host-adapter.md),
[request startup](follow-ups/request-startup-portability.md),
[runtime codegen](follow-ups/runtime-codegen-and-loading.md), and
[dynamic integration](follow-ups/dynamic-aspx-integration.md).
