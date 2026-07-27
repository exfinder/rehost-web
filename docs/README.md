# Documentation map

Read [`../PROJECT.md`](../PROJECT.md) first. Load only documents relevant to the
feature being changed. Code/tests are canonical for mechanics; these files
record contracts and rationale.

## Current contracts

| Area | Document |
| --- | --- |
| Managed runtime port plan | [core-runtime-port-plan.md](core-runtime-port-plan.md) |
| Classic runtime graph/lifetimes | [classic-managed-runtime-model.md](classic-managed-runtime-model.md) |
| Classic-path portability ledger | [portability-ledger.md](portability-ledger.md) |
| Application bootstrap/configuration | [application-bootstrap-and-configuration.md](application-bootstrap-and-configuration.md) |
| AppDomain, remoting, full trust | [appdomain-remoting-cas-compatibility.md](appdomain-remoting-cas-compatibility.md) |
| Application Services assembly/loader | [application-services-compatibility.md](application-services-compatibility.md) |
| Async/thread call context | [call-context-compatibility.md](call-context-compatibility.md) |
| Data protection base contract | [data-protector-compatibility.md](data-protector-compatibility.md) |
| Enterprise Services/COM+ | [enterprise-services-compatibility.md](enterprise-services-compatibility.md) |
| Generated System.Web inputs | [generated-build-inputs.md](generated-build-inputs.md) |
| Generated resource API | [generated-resource-contracts.md](generated-resource-contracts.md) |
| Remote IIS configuration | [remote-configuration-compatibility.md](remote-configuration-compatibility.md) |
| ResX behavior | [resx-reader-compatibility.md](resx-reader-compatibility.md) |
| SMTP configuration | [smtp-configuration-compatibility.md](smtp-configuration-compatibility.md) |
| Web Services configuration/scope | [web-services-compatibility.md](web-services-compatibility.md) |
| Windows administration/App_Browsers | [windows-administration-compatibility.md](windows-administration-compatibility.md) |
| XSD build provider | [xsd-build-provider-compatibility.md](xsd-build-provider-compatibility.md) |

Package rationale:
[dependency-decisions.md](dependency-decisions.md).
Source/licensing records:
[`provenance/`](provenance/).

## Current milestone

[First runnable request](follow-ups/first-runnable-request.md): execute one
configuration-mapped precompiled handler through the full classic managed
pipeline and Kestrel on every supported platform.

The [canonical port plan](core-runtime-port-plan.md) owns order and exit gates.
Follow-up files are scoped work packets, not independent architecture.

| Slice | Work | Status |
| ---: | --- | --- |
| 0 | [.NET Framework oracle](follow-ups/framework-differential-harness.md), root-config provenance, ledger | open |
| 1 | [First runnable request](follow-ups/first-runnable-request.md), including bootstrap reshape, host adapter, completion, response spool, and request startup | open |
| 2 | [Runtime codegen/loading](follow-ups/runtime-codegen-and-loading.md), `App_Code`, `Global.asax`, application start | open |
| 3 | [Dynamic ASPX integration](follow-ups/dynamic-aspx-integration.md), GET lifecycle/rendering | open |
| 4 | Request-body bridge, postback, view state, uploads | open |
| 5 | Built-in modules and services | open |
| 6 | Process drain/disposal and broader transport | open |

## Later backlog

| Priority | Work | Dependency |
| --- | --- | --- |
| High | [Data protection provider](follow-ups/data-protection-provider.md) | security/persistence ADR |
| High | [Process lifetime](follow-ups/process-lifetime-shutdown-and-recycle.md) | host lifecycle |
| High | [Request termination/timeouts](follow-ups/request-termination-and-timeouts.md) | pipeline cancellation |
| Medium | [AppDomain unload sites/`SYSLIB0024`](follow-ups/appdomain-unload-call-sites.md) | process lifetime |
| Medium | [Deferred request surfaces](follow-ups/deferred-request-surfaces.md) | first runnable request |
| Medium | [Hidden-file content selection](follow-ups/portable-filesystem-and-config-path-semantics.md) | runtime codegen/loading |
| Medium | [ResX edge cases](follow-ups/resx-compatibility.md) | trust/platform policy |
| Medium | [Route escaping](follow-ups/route-url-escaping.md) | Framework oracle |
| Medium | [WebResource timestamps](follow-ups/web-resource-assembly-timestamps.md) | publishing model |
| Low | [Enterprise Services scope](follow-ups/enterprise-services.md) | product profile |
| Low | [Configuration reload](follow-ups/configuration-reload-and-process-restart.md) | process lifetime |
| Low | [Generated resources](follow-ups/generated-resource-compatibility.md) | Framework oracle |
| Low | [Regex generation](follow-ups/regex-generation.md) | API/AOT policy |
| Low | [Web Services scope](follow-ups/web-services.md) | ASMX/SOAP profile |

## Build diagnostics

Use the non-hanging local build:

```text
dotnet build src/Rehost.WebForms.Runtime/Rehost.WebForms.Runtime.csproj --no-restore --disable-build-servers --nologo --verbosity:quiet --maxcpucount:1 /p:UseSharedCompilation=false /nodeReuse:false -clp:ErrorsOnly
```

When detailed diagnosis is needed, produce a temporary diagnostic log and run:

```text
python3 eng/analyze-build-diagnostics.py <log> <output-directory> --root .
```

Diagnostic TSV/JSON output is disposable. Do not commit build snapshots.
