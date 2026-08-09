# Roadmap

This file alone owns milestone order and current direction. Capability status
lives in [`docs/compatibility.md`](docs/compatibility.md); detailed unresolved
work lives in [`docs/backlog.md`](docs/backlog.md).

## Completed — portable runtime foundation

The retained classic managed pipeline now starts under a process-scoped host,
compiles and runs dynamic pages, and serves representative request, response,
page, control, async, upload, static-file, and IIS-derived configuration paths.
The former slices 0–4 are implementation history, not the roadmap.

## Current — Milestone 1: stock Visual Studio application

Run the unmodified .NET Framework 4.8.1 Visual Studio Web Forms template as a
real Web Application Project on .NET 10.

Application C#, markup, `Web.config`, and template assets are imported into this
repository and frozen. Sidecar SDK projects, host code, package substitutions,
and ported dependency assemblies are allowed. The application consumes locally
produced NuGet packages; project references into the runtime are not allowed.

Work follows the browser journey:

1. Package the runtime and compile the WAP code-behind, designer files, and
   `Global.asax` through an SDK-style sidecar.
2. Cold-start `/` and render the default page through normal handler mapping and
   default-document behavior.
3. Restore bundles, scripts, styles, and static assets.
4. Restore Friendly URLs and routing.
5. Restore mobile master selection and view switching.
6. Gate the same browser-visible journey on Windows x64, Linux x64, and macOS
   arm64.

The local `Rehost.WebForms.FriendlyUrls` package now covers steps 4–5 in a
focused hosted fixture. The frozen application's package build consumes it and
currently stops at missing `ScriptManager` APIs. Continue with ScriptManager,
Optimization/WebForms integration, WebGrease, and other managed `System.Web`
relatives only as reached. Their original managed sources remain preferred.

The next vertical slice keeps the frozen application tree unchanged and restores
only its full-page script/style path. It adds a source-compatible
`Rehost.WebForms.Extensions` ScriptManager closure, attempts a full compile of
the official ASP.NET Web Optimization source, ports its small WebForms
`BundleReference`, and replaces the two legacy ScriptManager startup helpers
with one `Rehost.WebForms.ScriptManager.Bundles` package. The existing physical
JS/CSS files remain application-owned. General AJAX, embedded script delivery,
and unrelated `System.Web.Extensions` APIs stay deferred. Detailed boundaries
and evidence are in the
[stock template script-stack plan](docs/follow-ups/stock-template-script-stack.md).

Done means the frozen template application builds from local packages and its
complete journey passes automatically on all three platforms. The existing
custom sample remains a personal visual playground, not milestone evidence.

## Next — Milestone 2: eShopLegacyWebForms

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
