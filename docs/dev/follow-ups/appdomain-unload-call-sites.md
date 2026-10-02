# AppDomain unload call sites and the SYSLIB0024 tripwire

One call site remains and the change is scoped. Overlaps
[process lifetime, shutdown, and recycle](process-lifetime-shutdown-and-recycle.md),
which owns the wider drain/recycle policy.

## Problem

`SYSLIB0024` is suppressed project-wide in
`src/Rehost.Web/Rehost.Web.csproj`, hiding every new
`AppDomain.Unload` use from a warnings-as-errors build.

## Current state

`Hosting/ProcessHost.cs:1240` is the only compiled Runtime call. The separate
Application Services project has its own call and suppression boundary.

## The remaining site

`ProcessHost.cs:1240` sits in `IProcessHostLite.ReportCustomLoaderError`, a COM
member invoked only by IIS `webengine` native code. No portable host can reach
it. Other live `ProcessHost` references prevent deleting the file wholesale.

## Proposed treatment

Treatment: **unsupported**. Wrap the member body in `#if NETFRAMEWORK`,
with `#else` throwing `CurrentAppDomainHosting.SecondaryAppDomainsUnsupported()`,
matching the precedent already set for `CreateObjectInNewWorkerAppDomain` at
`ApplicationManager.cs:836`. Then drop `SYSLIB0024` from `NoWarn`.

A site-local pragma would restore the tripwire but retain a dead unload path;
explicit unsupported behavior records the boundary.

## Done when

- `ProcessHost.cs:1240` is unreachable or explicitly unsupported.
- `SYSLIB0024` is absent from the `Rehost.Web` `NoWarn` list.
- The runtime still builds with warnings treated as errors.
- Any remaining suppression is narrow and documented at its site.
