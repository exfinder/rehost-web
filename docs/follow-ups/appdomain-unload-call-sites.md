# AppDomain unload call sites and the SYSLIB0024 tripwire

Status: open. Priority: medium. Ready to execute — one call site remains, and the
change is scoped. Overlaps
[process lifetime, shutdown, and recycle](process-lifetime-shutdown-and-recycle.md),
which owns the wider drain/recycle policy.

## Problem

`SYSLIB0024` is suppressed project-wide in
`src/Rehost.WebForms.Runtime/Rehost.WebForms.Runtime.csproj`. The suppression
entered in `bb4d758` ("fix(runtime): resolve final build warnings") together with
`TreatWarningsAsErrors`, and the same commit recorded its removal as debt. It is
deliberate, not accidental.

Because the suppression is project-wide, the compiled runtime is currently blind
to *every* `AppDomain.Unload` — including any newly introduced one. The next work
in this area is portable drain and recycle logic, which is exactly the code where
a reach for `AppDomain.Unload` should fail the build. The detector is worth most
immediately before that slice.

## Current state

The inventory in `ffaa90d` recorded three call sites. Two have since retired as a
side effect of unrelated portability work, neither of them targeting the warning:

| site | status |
| --- | --- |
| `Hosting/ApplicationManager.cs:1307` | behind `#if NETFRAMEWORK` (request-ownership slice) |
| `HttpRuntime.cs:1892` | behind `#if NETFRAMEWORK` (ledger P26) |
| `Hosting/ProcessHost.cs:1240` | **live** |

Removing `SYSLIB0024` from `NoWarn` was tried and fails on that one remaining
site alone.

`CustomLoaderHelper.cs:83` in `System.Web.ApplicationServices.ReferenceSource`
also calls `AppDomain.Unload`, but is a separate project and does not affect this
suppression.

## The remaining site

`ProcessHost.cs:1240` sits in `IProcessHostLite.ReportCustomLoaderError`, a COM
member invoked only by IIS `webengine` native code. No portable host can reach
it, so the body is provably unreachable rather than merely unused.

Deleting the file is **not** the cheap option — four live references survive:

- `Configuration/IISMapPath.cs:36` — `ProcessHost.DefaultHost`
- `Hosting/ISAPIApplicationHost.cs:48` — `ProcessHost.DefaultHost`
- `Hosting/ApplicationManager.cs:753` — `PreloadApplicationIfNotShuttingdown`,
  reached from `HostingEnvironmentShutdownInitiated` and guarded by
  `ac.PreloadContext != null`, so dead in practice but on the live shutdown path
- `Hosting/ApplicationManager.cs:952` — `GetExistingCustomLoaderFailureAndClear`

## Proposed treatment

Ledger treatment **unsupported**. Wrap the member body in `#if NETFRAMEWORK`,
with `#else` throwing `CurrentAppDomainHosting.SecondaryAppDomainsUnsupported()`,
matching the precedent already set for `CreateObjectInNewWorkerAppDomain` at
`ApplicationManager.cs:836`. Then drop `SYSLIB0024` from `NoWarn`.

A narrow `#pragma warning disable SYSLIB0024` at the site was the alternative
considered. It restores the tripwire with no behavior change at all, but leaves a
dead-yet-live unload call, so it records less than the explicit unsupported
member does.

Weigh before executing: this is process-lifetime slice scope pulled forward, and
having `ReportCustomLoaderError` throw rather than silently swallow is a
behavior change on a path no portable host can reach.

## Done when

- `ProcessHost.cs:1240` is unreachable or explicitly unsupported.
- `SYSLIB0024` is absent from the `Rehost.WebForms.Runtime` `NoWarn` list.
- The runtime still builds with warnings treated as errors.
- Any remaining suppression is narrow and documented at its site.
