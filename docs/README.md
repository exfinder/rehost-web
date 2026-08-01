# Documentation map

Read [`../PROJECT.md`](../PROJECT.md) first. Load only documents relevant to the
feature being changed. Code/tests are canonical for mechanics; these files
record contracts and rationale. Shared terms live in
[`../CONTEXT.md`](../CONTEXT.md).

## Accepted target design

These documents describe accepted direction; implementation may lag:

| Area | Document |
| --- | --- |
| Slice order and gates | [core-runtime-port-plan.md](core-runtime-port-plan.md) |
| Decision rationale | [`adr/`](adr/) |

## Implemented contracts

| Area | Document |
| --- | --- |
| Runtime graph, sequencing, and lifetimes | [classic-managed-runtime-model.md](classic-managed-runtime-model.md) |
| Application bootstrap/configuration | [application-bootstrap-and-configuration.md](application-bootstrap-and-configuration.md) |
| First runnable request | [follow-ups/first-runnable-request.md](follow-ups/first-runnable-request.md) |
| Runtime code generation/loading | [follow-ups/runtime-codegen-and-loading.md](follow-ups/runtime-codegen-and-loading.md) |
| Dynamic `.aspx` GET | [follow-ups/dynamic-aspx-integration.md](follow-ups/dynamic-aspx-integration.md) |
| Supported feature boundaries | [follow-ups/compatibility-feature-map.md](follow-ups/compatibility-feature-map.md) |
| Reached classic-path evidence | [portability-ledger.md](portability-ledger.md) |
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

Windows x64 validation host and sync workflow:
[windows-validation-host.md](windows-validation-host.md).
Package rationale:
[dependency-decisions.md](dependency-decisions.md).
Source/licensing records:
[`provenance/`](provenance/).
Framework configuration baseline:
[framework-config-reference.md](framework-config-reference.md).

## Current milestone

[Deferred request surfaces](follow-ups/deferred-request-surfaces.md), slice 4:
bridge request bodies, then add server forms, postback, view state, and uploads.
Slices 0–3 pass their gates on macOS `arm64` and Windows `x64`; the latest
completed slice compiles and renders a dynamic `.aspx` GET through Kestrel.

The [port plan](core-runtime-port-plan.md) owns ordering and cross-slice gates.
Each follow-up owns its status, dependencies, remaining decisions, and
acceptance criteria. Later work is under [`follow-ups/`](follow-ups/).
