# Migration reference

Detailed configuration, source, and runtime behavior for application ports.
Start with the [migration checklist](../migration.md) and use the sections below
when your application needs them.

## Configuration

The legacy `Web.config` is never edited. The build applies
`<Host>/Web.Rehost.config`, an XDT transform, and writes the result to
`rehost_root/web.config`; publish applies `Web.$(Configuration).config` first.
A `Web.Rehost.config` beside the Host replaces the package default wholesale, so
keep its adjustments (remove `<runtime>`, `<system.codedom>` and
`<system.serviceModel>`) and add the application's own below them. Typical
additions:

- connection strings for the environment (LocalDb is Windows-only; a named
  catalog on a server or a container replaces `AttachDbFilename`);
- removal of `<modules>` and `<handlers>` rows whose assembly cannot load here
  (Elmah, Application Insights, `Microsoft.AspNet.SessionState`), and of the
  `<location>` blocks that only registered them;
- shared `<machineKey>` values where multiple instances need the same keys
  (see Machine keys below).

Examples: [Wingtip Toys](../../apps/WingtipToys/DEVELOPMENT.md#webconfig),
[eShopLegacyWebForms](../../apps/eShopLegacyWebForms/DEVELOPMENT.md),
[YAF](../../apps/YAF/DEVELOPMENT.md#webconfig). Settings the runtime refuses fail
activation with a message naming the file and the entry; the list is in
[compatibility](compatibility.md).

Framework assembly names stay as the application wrote them. After the
transform, staging rewrites every staged `web.config`, the root one and each
nested one in any casing (`Views/Web.config`, an area's `Views/web.config`):
an `assembly` attribute naming an assembly below, and any attribute holding an
assembly-qualified type name from one, take the Rehost name with no version,
culture or public key token. The build logs each rewrite with its file and
line; every other byte, including namespace attributes, comments and
`<assemblyIdentity>`, is kept.

| Framework assembly | Rehost assembly |
| --- | --- |
| `System.Web` | `Rehost.Web` |
| `System.Web.Extensions` | `Rehost.Web.Extensions` |
| `System.Web.Services` | `Rehost.Web.Services` |
| `System.Web.ApplicationServices` | `Rehost.Web.ApplicationServices` |
| `System.Web.WebPages` | `Rehost.Web.WebPages` |
| `System.Web.WebPages.Razor` | `Rehost.Web.WebPages.Razor` |
| `System.Web.WebPages.Deployment` | `Rehost.Web.WebPages.Deployment` |
| `System.Web.Mvc` | `Rehost.Web.Mvc` |
| `System.Web.Http.WebHost` | `Rehost.Web.Http.WebHost` |
| `System.Web.Optimization` | `Rehost.Web.Optimization` |
| `Microsoft.AspNet.Web.Optimization.WebForms` | `Rehost.AspNet.Web.Optimization.WebForms` |
| `Microsoft.Web.Infrastructure` | `Rehost.Web.Infrastructure` |
| `Microsoft.Owin.Host.SystemWeb` | `Rehost.Owin.Host.SystemWeb` |

The runtime itself remaps these names only in `system.webServer` module and
handler types; configuration that does not pass through staging needs the
Rehost names written out.

## Preserved source

The App project's own `Compile` lines include C# source from the legacy folder
and leave markup for runtime compilation. `RehostAppContentRoot` is a plain
property the App project defines and uses for that folder, with a trailing
slash. The lines exclude `bin`, `obj`, and `packages`; the package adds no
`Compile` items.
Two differences from the old project's explicit file list:

- files on disk that the legacy project never listed are compiled too; add a
  `<Compile Remove>` line for each (YAF has four orphaned code-behind files);
- `App_Code` and `App_Data` source files are left to runtime compilation.
  `App_GlobalResources` and `App_LocalResources` are excluded from SDK resource
  embedding. A WAP that compiled `App_Code` into its assembly can encounter
  duplicate types (CS0433) if the same source is also compiled at runtime.

A Web Site project (no csproj, `CodeFile=` pages, code in `App_Code`) runs, but
the hosting targets stage a WAP: the `*.cs` sources are excluded from the copy,
and a Host-local target has to copy them. That is the state recorded in
[AjaxControlToolkitSampleSite](../../apps/AjaxControlToolkitSampleSite/DEVELOPMENT.md)
and the open
[project models](follow-ups/web-site-vs-wap-project-models.md) follow-up; the
template does not write a Web Site shape yet.

## Libraries bound to System.Web

A third-party binary that references the .NET Framework's `System.Web` fails at load
with a `FileNotFoundException` for `System.Web, Version=4.0.0.0,
PublicKeyToken=b03f5f7f11d50a3a`, or later with a type that cannot be cast.
There is no redirect to fix this; the assembly needs recompiling against this
runtime. The order of work that has held for six applications:

1. Probe the package as shipped from a throwaway .NET 10 console project,
   driving the code paths the application invokes, not only the reference.
   A package proven in one application can still fail in the next through a
   path the first never called.
2. Recompile only reached blockers and record imported revisions/licenses in
   [sources](sources.md), updated only on imports or upgrades. Katana's `Microsoft.Owin.Host.SystemWeb`, Web API's
   `Microsoft.AspNet.WebApi.WebHost` and `Microsoft.AspNet.WebPages` are the
   three shipped as `Rehost.Owin.Host.SystemWeb`,
   `Rehost.AspNet.WebApi.WebHost` and `Rehost.AspNet.WebPages`; the AJAX
   Control Toolkit, Autofac's Web integration and YAF's fourteen projects are
   rebuilt as source under `apps/`.
3. Expect a Framework facade now and then: Katana's Google provider constructs
   `System.Net.Http.WebRequest.WebRequestHandler`, a type .NET 10 does not
   carry, and a 15-line stand-in assembly closes it
   ([Wingtip Toys](../../apps/WingtipToys/DEVELOPMENT.md#webrequest-facade)).

## Custom build steps

Inventory `PostBuildEvent`, `BeforeBuild`/`AfterBuild` and custom targets in
every legacy project file before the first build. They ran invisibly on
Framework machines, and their output can be load-bearing and absent from source
control. The AJAX Control Toolkit sample site's static resources exist only as
the output of a Windows-only hard-link step; the port serves the same assets
embedded instead
([AjaxControlToolkitSampleSite](../../apps/AjaxControlToolkitSampleSite/DEVELOPMENT.md)).
Content generators (T4, `.resx` to designer, XSD data sets) need the same check;
the XSD build provider is
[unsupported](xsd-build-provider-compatibility.md).

## Language pins

The App project compiles the legacy sources with the C# 14 compiler. Old trees
compile unchanged in most cases, but two things change meaning:

- C# 14 lets implicit span conversions into overload resolution, so
  `array.Contains(x)` can bind to `MemoryExtensions` instead of `Enumerable`;
  code that builds expression trees from such calls then fails. Pin
  `<LangVersion>13</LangVersion>` for a rebuilt frozen tree
  ([YAF](../../apps/YAF/DEVELOPMENT.md#language-version)).
- `Nullable` and `ImplicitUsings` stay off in the App project, and
  `GenerateAssemblyInfo` is false because the tree has its own
  `AssemblyInfo.cs`. The template sets all three.

## Machine keys

Pick by deployment shape:

- **Single instance on a machine or a container with a mounted volume** —
  declare nothing. Auto-generated keys persist in a per-application key file
  (host `MachineKeyDirectory` option, default `~/.rehost/machine-keys`),
  so restarts keep ViewState, forms tickets, and Katana cookies valid
  ([ADR 0010](adr/0010-machine-key-persistence.md)).
- **Containers and farms** — keep key attributes auto-generated in config and
  supply the keys through `REHOST_MACHINEKEY_VALIDATIONKEY` and
  `REHOST_MACHINEKEY_DECRYPTIONKEY`. Each variable substitutes the
  whole attribute string before parsing, exactly as if written in web.config.
  Set both variables or neither — a lone one fails at startup, as does an
  explicit configured key alongside a set variable.
- **Explicit `<machineKey>` keys in web.config** — supported unchanged.

Settings that lived in the server's root web.config (commonly the algorithms)
move into the application's own web.config; the shipped root configs carry
stock Framework defaults only:

```xml
<machineKey validation="HMACSHA256" decryption="AES" />
```

This leaves the key attributes at their `AutoGenerate` default, which both the
persisted key file and the environment variables fill. The same config then
serves local development (persisted autogen keys) and production (env keys)
without edits.

Carrying over from Framework:

- Ticket/ViewState continuity through the cutover requires the *effective*
  old values: explicit keys and algorithms as production actually resolved
  them, not as remembered. With `AutoGenerate`, there is nothing to carry —
  the DPAPI registry blob is unreadable here, outstanding payloads invalidate
  once, and users re-authenticate.
- `,IsolateApps` / `,IsolateByAppId` suffix derivation across runtimes is
  unassessed; mixed Framework/port farms must use bare keys
  ([machine key](follow-ups/machine-key-and-viewstate-bootstrap.md)).

## Generated output (codegen)

Framework's `Temporary ASP.NET Files` becomes a per-application directory the
host resolves at startup. Pick by deployment shape:

- **Local development and plain servers** — declare nothing. Output lands in
  `~/.rehost/codegen/<site>/<hash>` under the user profile, beside the
  machine keys; the startup log names the exact directory (event 13). It is
  safe to delete (the next start recompiles) and an unchanged application
  restarts without recompiling ([ADR 0008](adr/0008-codegen-storage.md)). Do
  not delete the sibling `machine-keys` directory with it: that invalidates
  every auto-generated key. Keep the root outside the application: a root
  under the application's `bin` feeds the hash that decides reuse, so every
  restart recompiles, and preflight warns once (event 14).
- **Containers** — a read-only root filesystem refuses the default at boot.
  Point `REHOST_COMPILATION_TEMPDIRECTORY` (or the host
  `CompilationTempDirectory` option) at a mounted writable path.

Say it in at most one place: the option, the variable, and a web.config
`<compilation tempDirectory>` that disagree fail at startup naming both
sources.

Carrying over from Framework: a `tempDirectory` attribute holding a Windows
path fails on Linux/macOS — remove it and use the default or the variable.

## The base directory

`AppDomain.CurrentDomain.BaseDirectory` and `AppContext.BaseDirectory` are the
site root with a trailing separator, as on Framework, from the first Web Forms
request on. Until then they are the folder holding the host's binaries, the
site's `bin/`.

- Host code that needs the binaries' folder later (an options callback that
  runs on a request, for example) reads `AppContext.BaseDirectory` into a local
  at the top of `Program.cs`.
- Application code that combines `BaseDirectory` with
  `AppDomain.RelativeSearchPath` to find `bin` gets the site root instead,
  because modern .NET never sets `RelativeSearchPath`. Use
  `HttpRuntime.BinDirectory`.

## Root configuration files

The runtime ships its own machine.config and root web.config (structurally
derived from Framework 4.8.1) in `configs` beside the host binaries. They are
frozen: there is no override, matching Framework, where the framework-install
root configs were outside an application's reach.

Anything your hosting environment carried in machine.config, the server's root
web.config, or applicationHost.config moves into the application's own
web.config, which inherits from the shipped baselines section by section —
the same channel Framework apps already used. The machine-key and codegen
topics above are instances of this rule.

## App and Host layout

The Host stages content under `rehost_root/` and sets
`OutDir=rehost_root/bin/`; shared targets verify that location. Debug and Release
share that development output. Publish uses a separate site tree with content at
its root and payload under bin. Clear an earlier publish directory before checking
for obsolete files; publish does not remove them automatically.

`dotnet publish -o X` and `RehostPublishSiteRoot=X/` both select the site's root;
conflicting destinations fail. Stage manifests are scoped per root so publishing
to one destination cannot remove another destination's content. Configuration XDT
runs before Rehost XDT only during publish.

Host `ContentRootPath` points at the binary directory; Web Forms PhysicalRootPath
points at its parent. Read both from the startup binary location before activation.
The rationale is in [App/Host layout](adr/0014-app-host-layout.md).
