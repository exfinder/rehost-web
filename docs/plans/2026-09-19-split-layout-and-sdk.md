# Split layout and the two SDK packages

Goal: before the public alpha, an application builds and publishes as an
ASP.NET Core host that carries a Web Forms site, the way IIS and the GAC carried
one on Framework. Host and runtime files sit flat beside `Host.dll`; the
application and its libraries sit in `rehost_root/bin/`; content sits in
`rehost_root/`. Two MSBuild SDK packages reduce each csproj to its references.

Evidence for every mechanism below is in
[migration stages](../follow-ups/migration-stages-and-site-layout.md) and
[Rehost SDK](../follow-ups/rehost-sdk.md). Read both before starting a phase.

## Target shape

```text
MyApp.Host/
  bin/Debug/net10.0/        Host.dll, Rehost.WebForms.*.dll, Roslyn, configs/,
                            Host.deps.json, Host.runtimeconfig.json, appsettings.json
  rehost_root/              git-ignored while the Framework project is alive
    bin/                    MyApp.dll, its libraries, MyApp.deps.json
    *.aspx, web.config, Content/, ...
  Program.cs
  Web.Rehost.config
MyApp.App/                  compiles the legacy *.cs into rehost_root/bin/
MyApp/                      the frozen Framework project
```

Published: `X/` holds the Host flat, `X/rehost_root/` the content,
`X/rehost_root/bin/` the application.

## Decisions

Settled with the user on 2026-09-19. An implementer does not reopen them.

1. `AppDomain.CurrentDomain.BaseDirectory` is the site root, as on Framework.
   The runtime sets `APP_CONTEXT_BASE_DIRECTORY` when the Web Forms application
   starts, never earlier, so everything ASP.NET Core set up before that keeps
   the real folder. Runtime code that needs the host folder reads a captured
   value, not `AppContext.BaseDirectory`. No opt-out; one backlog line.
2. The application resolver is installed before `Main`, from a generated
   `[ModuleInitializer]` file that the hosting package targets add to the Host
   compile (both csproj forms). The runtime's own install stays as a safety net.
3. The build writes `Rehost.WebForms.SiteRoot` into `Host.runtimeconfig.json`:
   the full path of `rehost_root/` for a build, `rehost_root` for a publish.
   `options.PhysicalRootPath` stays as an override. A missing folder fails at
   start naming the file and the setting.
4. Nobody trims `rehost_root/bin/`. The runtime's files land there as unused
   duplicates. Trimming (the Host deletes what its own folder holds) is a later
   improvement, to be decided together with its effect on page references.
5. The SDK adds the core package references with its own version and sets
   `$(RehostWebFormsVersion)`; add-ons stay in the csproj.
   `RehostImplicitPackageReferences=false` turns it off.
6. The split is the only layout. `RehostVerifyOutDir`, the `-o` redirect and
   `IsWebConfigTransformDisabled` are removed; the per-root stage manifest stays.
7. The SDK is the main way; the long csproj is a supported fallback, and the
   checks live in the package targets. WingtipToys stays on the long form. YAF
   uses the SDK.
8. A Web Site gets an App project with no sources that carries the references
   belonging in `bin/` (AjaxControlToolkitSampleSite).
9. `samples/Rehost.WebForms.SampleApp` is out of scope and unchanged. It must
   still build and serve `Default.aspx`.

Carried from the prototypes: `AssemblyDependencyResolver` over each
`bin/*.deps.json` ahead of the file-name probe; the Host references the App with
`Private="false" ExcludeAssets="runtime;native"`; the App marks every direct
project reference `PrivateAssets="all"`; SDK ids `Rehost.WebForms.Sdk.App` and
`Rehost.WebForms.Sdk.Host`; a csproj value wins over an SDK default.

## Plan-level choices

Made while writing this plan, not grilled. The user may veto any of them.

- Two public members on `WebFormsApplication`: `InstallApplicationAssemblyResolver()`
  (idempotent, no parameters; the generated file calls it, and a Host without
  the package targets calls it by hand) and `DefaultPhysicalRootPath` (the
  resolved `Rehost.WebForms.SiteRoot`, for a `Program.cs` that needs the site
  path, as the Identity host does for `App_Data`).
- The package targets recognize an App project by `EnableDynamicLoading=true`
  in a project that imports the runtime package targets. No new marker property.
- The Host targets learn an App reference's `OutDir` by calling a target the
  runtime package targets define on it (`SkipNonexistentTargets`), not by
  naming convention. The SDK alone uses the `*.App.csproj` pattern to default
  the reference metadata.

## Phases

One implementer at a time. A phase ends with the runtime and hosting test
projects green and all six app smokes green on macOS, then a commit on `main`.

### 1. Runtime

- `GeneratedAssemblyLoader`: one `AssemblyDependencyResolver` per
  `bin/*.deps.json` with a matching `.dll`, skipping the entry assembly's own
  file; `Resolving` asks the resolvers, then the existing file-name probe;
  `ResolvingUnmanagedDll` asks the resolvers. Installing early with `bin`
  alone and later adding the codegen directory must both work.
- The two public members above. `PhysicalRootPath` becomes optional: absent, it
  is the setting; both absent fails naming both.
- The base-directory switch, after a winbox reading of the exact Framework value
  (IIS Express, trailing separator or not; record it with the ledger row).
  Captured host folder for `configs/`, `RoslynCSharpCompiler` and the OWIN
  loader. `RoslynCSharpCompiler`'s out-of-band lookup checks the host folder,
  then the site `bin`.
- YAF's module scanner adds `RelativeSearchPath`, which modern .NET never sets,
  so with the site root it finds no provider. YAF gets its fix in this phase,
  by whatever means `apps/YAF` already uses for a frozen file that cannot
  compile or run as is. No precedent means stop and ask.
- Tests follow `docs/writing-tests.md`; a resolver test must fail when the
  resolvers are removed (a per-OS asset layout is the input that does).

### 2. Package targets and the long form

- Hosting targets: content and transformed `web.config` to `rehost_root/`; no
  payload copy, no `OutDir` rule, no run redirection; the `SiteRoot` runtime
  option; the generated initializer file; a build error for an App reference
  without `Private="false" ExcludeAssets="runtime;native"` and for an App whose
  `OutDir` is not `<stage>/bin/`. Runtime package targets: a build error for an
  App project reference without `PrivateAssets="all"`.
- Publish: Host to `X/` as the SDK does it; content to `X/rehost_root/` with
  `Web.$(Configuration).config` first; the App published to
  `X/rehost_root/bin/` with the Host's runtime identifier passed on; the
  published `runtimeconfig.json` carries `rehost_root` for `dotnet publish` and
  for `msbuild -t:Publish`. The Web SDK's `web.config` at `X/` is left alone.
- All six apps move to the long form. `Program.cs` loses `PhysicalRootPath`.
  AjaxControlToolkitSampleSite gains its source-less App project.

### 3. SDK packages

- `src/Rehost.WebForms.Sdk.App` and `src/Rehost.WebForms.Sdk.Host`, packed with
  the other packages at the same version. The prototype's four files are in
  the session scratchpad under `sdk-prototype-keep/`; the coordinator supplies
  them. The content-root defaults apply only when the conventional folder exists.
- Five apps move to the SDK; WingtipToys stays long.
- The NuGet SDK resolver reads `NuGet.config` and runs at evaluation, before the
  local feed is packed on a clean clone. "One command is always enough"
  (`apps/WebFormsApplication/README.md`) must stay true. No way to keep it means
  stop and ask.

### 4. Proof and documents

- `eng/external-consumer.sh` in the SDK form, starting the published folder and
  running the smoke against it.
- ADR 0014 for the layout and loading; the migration-stages document becomes
  the current contract; `docs/migration.md`, `docs/compatibility.md`, the app
  READMEs, ledger rows; backlog lines for the base-directory opt-out and the
  `bin/` trim.

### 5. Gate (coordinator)

Runtime and hosting suites, all six app smokes, and the external-consumer
script on macOS, Linux and Windows; the sample app builds and serves
`Default.aspx`; then a two-axis review.

## Rules for implementers

- Any step that cannot be done as written, or any choice this plan does not
  make, ends the turn with a section titled `QUESTION FOR USER`. No workaround,
  no silent substitute. The coordinator takes it to the user.
- `AGENTS.md`, `docs/code-style.md` and `docs/writing-tests.md` apply in full.
  No comments unless they name a hidden constraint. No `Co-Authored-By` footer.
- Stage files by path; never `git add -A` (the user keeps untracked folders in
  the checkout). Never `git stash`. Commit to `main`.
- Never delete or recreate a Docker container. `rehost-wingtip-sql` and
  `rehost-yaf-pg` are running; the YAF installer needs an empty database, and
  dropping and recreating the `yafnet` database inside `rehost-yaf-pg` is allowed.
- Hosts started by hand are stopped by PID before the turn ends.
- After editing a package's `build/` or `Sdk/` files, run `dotnet restore` on
  the app first: the first build repacks the feed but still evaluates the old
  files. Delete a publish folder before judging its contents.
- No Windows or Linux rounds; the coordinator runs the gate.
