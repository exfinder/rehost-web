# AppDomain, remoting, and CAS compatibility

## Contract

- One Web Forms application and one `HostingEnvironment` per OS process.
- The host calls `WebFormsApplication.Initialize` before application code,
  `HostingEnvironment`, or first `HttpRuntime` access.
- Execution is always full trust.
- Current-AppDomain request processing, configuration, runtime compilation,
  local application inspection, and local object registration remain supported.
- `ObjectHandle` may wrap a local reference only; it provides no proxy,
  serialization, lease, or isolation.
- Dynamic assemblies are in-process, executable, and non-collectible.

Explicitly unsupported:

- secondary AppDomains, multiple isolated applications per process, AppDomain
  unload, Fusion shadow-copy behavior, and static-state reset;
- remoting proxies, channels, leases, remote activation, and callbacks;
- `ApplicationHost.CreateApplicationHost`, worker-domain
  `ApplicationManager` paths, and `ClientBuildManager`;
- partial trust, CAS policy/enforcement, `legacyCasModel`, and security
  resolver/assembly-list behavior;
- claiming `AssemblyLoadContext` as AppDomain, remoting, or CAS compatibility.

Unsupported entry points fail with actionable
`PlatformNotSupportedException`. Missing/default/explicit `Full` trust starts;
other trust levels and `legacyCasModel="true"` fail during initialization.

## Rationale

Modern .NET has no secondary AppDomains, remoting, or CAS enforcement.
`AssemblyLoadContext` supplies assembly loading, not the isolation, lifecycle,
proxy, or security contracts expected by System.Web. Process isolation is the
only honest replacement.

The implementation keeps viable local behavior and rejects contracts that
require unavailable boundaries. Relevant source is under
`src/System.Web.ReferenceSource/Hosting`, `Compilation`, `HttpRuntime.cs`, and
`src/Rehost.WebForms.Runtime/Compatibility`.

Application identity, roots, configuration sources, and single-attempt process
binding are defined by
[application bootstrap](application-bootstrap-and-configuration.md).

Authorities:

- [Unavailable .NET Framework technologies](https://learn.microsoft.com/dotnet/core/porting/net-framework-tech-unavailable)
- [Modern `AppDomain` source](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/AppDomain.cs)
- [CAS diagnostic SYSLIB0003](https://learn.microsoft.com/dotnet/fundamentals/syslib-diagnostics/syslib0003)
- [Reference Source provenance](provenance/reference-source.json)

Shutdown/restart remains open:
[process lifetime](follow-ups/process-lifetime-shutdown-and-recycle.md).
