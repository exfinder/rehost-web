# Completed milestones

These notes record completed validation work. Current priorities live in the
[roadmap](../ROADMAP.md). For current application setup, use each application's
README; exact support claims live in the
[detailed compatibility reference](compatibility.md).

## Portable runtime foundation

The retained classic managed pipeline now starts under a process-scoped host,
compiles and runs dynamic pages, and serves representative request, response,
page, control, async, upload, static-file, and IIS-derived configuration paths.
The former slices 0–4 are implementation history, not the roadmap.

## Milestone 1: stock Visual Studio applications

Both .NET Framework 4.8.1 Visual Studio Web Forms templates run unmodified as
real Web Application Projects on .NET 10, from locally produced NuGet packages,
on Windows x64, Linux, and macOS arm64:

- [`apps/WebFormsApplication`](../apps/WebFormsApplication/README.md) — the plain
  template. `smoke.sh` walks the browser journey (default document and its
  postback target, Friendly URLs, the `.aspx` redirect, script and style
  bundles, static assets, mobile master and view switching).
- [`apps/WebFormsIdentityApplication`](../apps/WebFormsIdentityApplication/README.md)
  — the "Individual User Accounts" template: OWIN (Katana recompiled as
  `Rehost.Owin.Host.SystemWeb`), ASP.NET Identity 2.2 and Entity
  Framework 6.4 consumed from nuget.org, SQL Server in a container. `smoke.sh`
  matches every row of the IIS Express baseline (register, log in, log off,
  URL authorization challenge, cookies).

Each app is a frozen tree plus a sidecar `.App`/`.Host` pair; the packages it
consumes are packed from `src/` into a shared local feed. Boundaries recorded
along the way: LocalDb is a Windows-only engine (the connection string is the
one app-visible change). Auto-generated keys later gained host-resolvable
file persistence ([ADR 0010](adr/0010-machine-key-persistence.md)); the
`ListView`/`DataPager` family joined the Extensions closure and Dynamic Data
stays absent (ledger P69). The gap analysis and its closure live in
[`docs/research/webforms-identity-application-gaps.md`](research/webforms-identity-application-gaps.md).

## Milestone 2: eShopLegacyWebForms

Microsoft's eShopLegacyWebForms runs with its C#, markup, and `Web.config`
frozen, on mock data, from [`apps/eShopLegacyWebForms`](../apps/eShopLegacyWebForms/README.md).
The catalog journeys (the application has no purchase flow) pass on all three
platforms; closure work landed as two XDT module drops, an
`Autofac.Integration.Web` recompile, the baseline `expressionBuilders`
registration, and a script-mapping shim. The AJAX Control Toolkit spike rode
the same patterns: the toolkit recompiles against the port and its 50-page
sample site runs as a Web Site project
([`apps/AjaxControlToolkitSampleSite`](../apps/AjaxControlToolkitSampleSite/README.md)).

## Milestone 3: Wingtip Toys

The frozen Wingtip Toys tutorial store runs its stateful commerce journey on
all three platforms against containerized SQL Server, from
[`apps/WingtipToys`](../apps/WingtipToys/README.md): Identity 2.2 register and
sign-in, claims-role authorization, the session-keyed database cart, and
checkout through the order write against a local NVP responder standing in
for PayPal — with `customErrors` as authored, zero frozen-source edits, zero
`src/` changes, and no library recompiles. Provider behavior
(membership/role/profile providers) proved structurally unreachable here: the
application clears every provider section and runs roles from Identity
claims, so the SQL-provider journey stays in the backlog.

Wingtip also exercises production bundle combination and minification.
Broader Optimization caching and `VaryBy` behavior remain in the backlog.

## Integrated-divergence closure

Every open item of the
[integrated-vs-classic divergence audit](research/integrated-divergence-audit.md)
is closed: the migrating audience ran integrated mode, and each divergence now
ends as integrated behavior or a fail-fast diagnostic naming the boundary —
never silence. Six jobs, one per session, landed as ledger P90-P93.
