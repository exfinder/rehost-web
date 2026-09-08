# Bringing up an application

The loop that took both Visual Studio templates from a Windows checkout to a
three-platform journey. Each step produces a durable artifact; the next step
consumes it. Skip none: every gap the second template exposed was found by the
step designed to find it.

1. **Import frozen.** Copy the application tree unmodified into
   `apps/<App>/<App>/` (no `bin/`, `obj/`, `packages/`, `.sln`, local
   databases). It stays buildable in Visual Studio; nothing here edits it.
2. **Map the gaps.** `docs/research/<app>-gaps.md`: what the app touches per
   dependency, whether a `Rehost.*` package exists, whether the shipped binary
   runs off Windows, and a proposed direction — port, consume as-is,
   substitute, out of contract — marked as proposal until accepted.
   Inventory build customizations in every upstream project file —
   `PostBuildEvent`, `BeforeBuild`/`AfterBuild`, custom targets, content
   generators. They run invisibly on Framework machines and their output can
   be load-bearing yet absent from source control; the AjaxControlToolkit
   sample site's static resources exist only as an unconditional post-build
   hard-link step.
3. **Take the Framework baseline.** Run the app on IIS Express (integrated
   pool) from a disposable copy and record the observable rows the port must
   match: status codes, `Location`, cookies, body markers, generated control
   names. This table is the oracle for the smoke script later.
4. **Probe third-party packages as shipped.** A throwaway console project on
   .NET 10 referencing the exact `net45` packages under `NU1701`, driving
   their API directly. It answers "consume as-is or recompile?" per package
   before any port work; only assemblies bound to Microsoft's `System.Web`
   identity need recompiling. Probe the code paths the application actually
   invokes, not the reference: a package proven in one app can still fail in
   the next through an uninvoked path (Katana's Google provider constructs a
   Framework-only `System.Net.Http.WebRequest` type that referencing alone
   never touches).
5. **Port only proven blockers.** Recompile a System.Web consumer verbatim
   with a provenance record; measure the compile against the runtime first
   (the Katana host was 4 errors in 7.8k lines). A runtime seam the app
   exposes gets IIS readings before design (`Response.Headers`, ledger P68),
   lands as a portable leaf with a ledger row, and is tested at unit and wire
   level. Fixes with a clear Framework answer land on their own with a test
   that is red without them; anything needing a design decision stops and is
   surfaced with evidence.
6. **App/Host pair.** `apps/<App>/<App>.App` compiles the frozen tree from
   packages, `<App>.Host` is the ~30-line Kestrel host, `Web.Rehost.config`
   beside the host holds the app's XDT (connection strings, assembly swaps).
   Consumer-shaped: local feed for `Rehost.*`, nuget.org for the rest.
7. **Smoke on three platforms.** `apps/<App>/smoke.sh` (bash + curl) asserts
   every baseline row and posts to the *rendered* form action, not a hardcoded
   URL; `eng/app-linux-smoke.sh <App> <port>` runs it in a container; the
   Windows validation clone runs it under Git bash. Only then does the
   compatibility map claim the app.
8. **Record.** Strike the gap document's steps, list every gap the pair
   exposed with its commit, update compatibility rows and the roadmap.

## What bites on the rebuild

Not app bugs. These are ways a tree that built on net481 means something else
once the same sources go through a modern compiler and BCL, and each cost a
debugging round on YAF.NET ([app notes](../apps/YAF/README.md)).

- **The frozen tree has its own build files.** Sidecars that glob sources sit
  outside them, so `Directory.Build.props` beside the vendored code never
  applies and its `DefineConstants` are silently gone. YAF's vendored OrmLite
  needs `NETFX` to pick a `PclExport`; without it every query died in a static
  constructor, at runtime, long after a clean compile. Read every
  `Directory.Build.*` under the import before writing a sidecar.
- **The language version floats with the SDK.** A missing `LangVersion` means
  7.3 on any `net4x` project, old-style or SDK-style, and 14 the moment a
  sidecar retargets the same sources to `net10.0`; `latest` and `preview`
  float the same way. Pin each sidecar to what its upstream project built
  with: the number it names, 7.3 when it names nothing, and for `latest` or
  `preview` the compiler its tool markers imply (`global.json`, a
  `Microsoft.Net.Compilers` package, the highest `VisualStudioVersion` group in
  the csproj; VS 2019 and 2022 default to 7.3, `latest` on VS 2022 is 12 or
  13, on VS 2026 it is 14). One pin per project, never `latest`; raise a
  project only when a file fails to compile, and name that file. The trap is
  not the compiler alone but the compiler plus the modern BCL: C# 14 lets
  arrays convert to spans during overload resolution, and the .NET 10 BCL has
  a generic `MemoryExtensions.Contains` that Framework's `System.Memory` lacks,
  so `array.Contains(x)` silently rebinds from `Enumerable` on the retargeted
  build only. In plain code that is harmless. Inside an expression tree the
  `ReadOnlySpan` node is a ref struct, `Expression.Compile()` throws
  `InvalidProgramException`, and the build was clean. YAF's OrmLite predicate
  over a `string[]` in `UserStore` hit it on the installer's first query; its
  sidecars pin 13 because upstream said `latest` on VS 2026, and one project
  takes 14 for a file using the `field` keyword. A clean build does not prove
  the pin; run the smoke journey.
- **Framework's parameterless constructors read configuration.**
  `new SmtpClient()` took host, port, credentials and delivery method from
  `<system.net><mailSettings>`; modern .NET deleted that reading entirely, so
  the same call yields an unconfigured client
  ([reading](follow-ups/system-net-mail-settings.md)). Treat any
  configuration-driven BCL type as unconfigured until measured.
- **Reflection is stricter.** Framework let `FieldInfo.SetValue` write a static
  `initonly` field; .NET refuses once the type is initialized. An application
  that reaches into a library's private statics fails where it used to work.
- **A case difference only one platform can see.** YAF names a `configSource`
  in a casing the file does not have. NTFS and stock APFS fold it; ext4 does
  not, and the section failed to load. The port folds that seam now (ledger
  P70), but the class stays open: any path an application spells itself can
  differ this way. The Linux round is what catches it; nothing earlier in the
  loop can.

Rigs: [Windows validation](windows-validation-host.md) (IIS Express readings,
Docker Desktop for SQL Server), `eng/linux-round.sh` and
`eng/app-linux-smoke.sh` for Linux, SQL Server as
`mcr.microsoft.com/mssql/server:2022-latest` where an app needs a database.
