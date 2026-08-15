# Roadmap

This file alone owns milestone order and current direction. Capability status
lives in [`docs/compatibility.md`](docs/compatibility.md); detailed unresolved
work lives in [`docs/backlog.md`](docs/backlog.md).

## Completed — portable runtime foundation

The retained classic managed pipeline now starts under a process-scoped host,
compiles and runs dynamic pages, and serves representative request, response,
page, control, async, upload, static-file, and IIS-derived configuration paths.
The former slices 0–4 are implementation history, not the roadmap.

## Completed — Milestone 1: stock Visual Studio applications

Both .NET Framework 4.8.1 Visual Studio Web Forms templates run unmodified as
real Web Application Projects on .NET 10, from locally produced NuGet packages,
on Windows x64, Linux, and macOS arm64:

- [`apps/WebFormsApplication`](apps/WebFormsApplication/README.md) — the plain
  template. `smoke.sh` walks the browser journey (default document and its
  postback target, Friendly URLs, the `.aspx` redirect, script and style
  bundles, static assets, mobile master and view switching).
- [`apps/WebFormsIdentityApplication`](apps/WebFormsIdentityApplication/README.md)
  — the "Individual User Accounts" template: OWIN (Katana recompiled as
  `Rehost.WebForms.Owin.Host.SystemWeb`), ASP.NET Identity 2.2 and Entity
  Framework 6.4 consumed from nuget.org, SQL Server in a container. `smoke.sh`
  matches every row of the IIS Express baseline (register, log in, log off,
  URL authorization challenge, cookies).

Each app is a frozen tree plus a sidecar `.App`/`.Host` pair; the packages it
consumes are packed from `src/` into a shared local feed. Boundaries recorded
along the way: LocalDb is a Windows-only engine (the connection string is the
one app-visible change); auto-generated machine keys are process-scoped, so
logins do not survive a restart without an explicit `<machineKey>`; the
`ListView`/`DataPager` family joined the Extensions closure and Dynamic Data
stays absent (ledger P69). The gap analysis and its closure live in
[`docs/research/webforms-identity-application-gaps.md`](docs/research/webforms-identity-application-gaps.md).

## Current — Milestone 2: eShopLegacyWebForms

Run Microsoft's eShopLegacyWebForms application with its C#, markup, and
`Web.config` frozen. Start with deterministic mock data so runtime compatibility
is separable from database operations. EF6 may be consumed as a modern NuGet
dependency; application-specific substitutions remain sidecar/package work.

Done means the application's principal browsing and purchase journeys run on
all three platforms with mock data and no application-source rewrite.

## Later — Milestone 3: production baseline

Turn the eShop result into a production deployment baseline: real SQL,
deterministic Windows/Linux publish, containers, graceful lifecycle,
readiness/health, metrics and diagnostics, external configuration and secrets,
and multi-instance operation.

## Horizon — Milestone 4

Use Wingtip Toys to drive stateful commerce breadth: authentication, session,
cart, and related provider behavior. Define exact scope only when Milestone 3 is
near completion.

## Rejected directions

- Full `System.Web` completeness as a prerequisite to application progress.
- Binary drop-in compatibility with Microsoft's strong-named assemblies.
- A Windows-only runtime profile.
- Rewriting the milestone applications to fit the port.
- Detailed Milestone 4 planning now.
