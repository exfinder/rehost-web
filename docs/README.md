# Documentation map

Read [`../PROJECT.md`](../PROJECT.md) first. Load only documents relevant to the
feature being changed. Code/tests are canonical for mechanics; these files
record contracts and rationale.

## Current contracts

| Area | Document |
| --- | --- |
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

## Active follow-ups

| Priority | Work | Dependency |
| --- | --- | --- |
| High | [First runnable request](follow-ups/first-runnable-request.md) | phase-one runtime build |
| High | [ASP.NET Core host adapter](follow-ups/aspnet-core-host-adapter.md) | host-neutral boundary |
| High | [Request completion](follow-ups/request-completion-failure-and-cancellation.md) | host adapter |
| High | [Application bootstrap](follow-ups/application-bootstrap-and-configuration.md) | host lifecycle/configuration |
| High | [Portable path mapping](follow-ups/portable-path-mapping-and-containment.md) | host filesystem |
| High | [Portable filesystem semantics](follow-ups/portable-filesystem-and-config-path-semantics.md) | path mapping |
| High | [Request-startup portability](follow-ups/request-startup-portability.md) | supported-path reachability |
| High | [Managed response output](follow-ups/managed-response-buffering-and-output.md) | host adapter |
| High | [Minimal pipeline profile](follow-ups/minimal-pipeline-feature-profile.md) | configuration/bootstrap |
| High | [Compiler policy](follow-ups/compiler-provider-and-target-framework-policy.md) | runtime codegen/loading |
| High | [Machine key/ViewState bootstrap](follow-ups/machine-key-and-viewstate-bootstrap.md) | security/persistence policy |
| High | [Runtime process policy](follow-ups/runtime-process-policy.md) | host lifecycle |
| High | [Portable request diagnostics](follow-ups/portable-request-diagnostics.md) | request pipeline |
| High | [Dynamic ASPX integration](follow-ups/dynamic-aspx-integration.md) | first-request implementation stories |
| High | [Compatibility feature map](follow-ups/compatibility-feature-map.md) | explicit support decisions |
| High | [Data protection provider](follow-ups/data-protection-provider.md) | security/persistence ADR |
| High | [Process lifetime](follow-ups/process-lifetime-shutdown-and-recycle.md) | host lifecycle |
| High | [Request termination/timeouts](follow-ups/request-termination-and-timeouts.md) | pipeline cancellation |
| High | [Runtime codegen/loading](follow-ups/runtime-codegen-and-loading.md) | host filesystem |
| High | [Windows diagnostics](follow-ups/windows-platform-diagnostics.md) | reachability audit |
| Medium | [Deferred request surfaces](follow-ups/deferred-request-surfaces.md) | first runnable request |
| Medium | [.NET Framework differential harness](follow-ups/framework-differential-harness.md) | runnable fixtures |
| Medium | [ResX edge cases](follow-ups/resx-compatibility.md) | trust/platform policy |
| Medium | [Route escaping](follow-ups/route-url-escaping.md) | Framework oracle |
| Medium | [WebResource timestamps](follow-ups/web-resource-assembly-timestamps.md) | publishing model |
| Low | [Enterprise Services scope](follow-ups/enterprise-services.md) | product profile |
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
