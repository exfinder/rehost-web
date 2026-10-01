# Migrating an application

The walkthrough after the first run. [Getting started](getting-started.md)
gets the stock template to its first page; this page takes a real application
the rest of the way, in the order the problems appear. Each section is short
and links the application under `apps/` where the evidence and the full
reasoning live. [Compatibility](compatibility.md) owns the support claims.

## Package mapping

Every `packages.config` line becomes a `PackageReference` in the App project,
or goes. A package built on `System.Web` binds to Microsoft's strong-named
assembly, so it compiles and then fails at run time. It takes its Rehost
counterpart, which carries the original name with `Microsoft` changed to
`Rehost`. The in-box assemblies follow the same rule with `System`. A line that
a counterpart already brings goes.

| Legacy reference | Rehost |
| --- | --- |
| `System.Web`, `System.Web.Extensions`, `System.Web.ApplicationServices`, `System.Web.Services` (in-box) | `Rehost.Web`, which ships the assemblies `Rehost.Web`, `Rehost.Web.Extensions`, `Rehost.Web.ApplicationServices` and `Rehost.Web.Services` |
| `Microsoft.AspNet.FriendlyUrls` | `Rehost.AspNet.FriendlyUrls` |
| `Microsoft.AspNet.FriendlyUrls.Core` | drop, covered by `Rehost.AspNet.FriendlyUrls` |
| `Microsoft.AspNet.Web.Optimization` | `Rehost.AspNet.Web.Optimization` |
| `Microsoft.AspNet.Web.Optimization.WebForms` | `Rehost.AspNet.Web.Optimization.WebForms` |
| `WebGrease`, `Antlr` | drop, covered by `Rehost.AspNet.Web.Optimization` |
| `Microsoft.AspNet.ScriptManager.MSAjax` | `Rehost.AspNet.ScriptManager.MSAjax` |
| `Microsoft.AspNet.ScriptManager.WebForms` | `Rehost.AspNet.ScriptManager.WebForms` |
| `AspNet.ScriptManager.jQuery`, `AspNet.ScriptManager.bootstrap` | drop, re-register by hand |
| `Microsoft.Owin.Host.SystemWeb` | `Rehost.Owin.Host.SystemWeb` |
| `Microsoft.Owin`, `Owin` | drop, covered by `Rehost.Owin.Host.SystemWeb` |
| `Microsoft.Web.Infrastructure` | drop, covered by `Rehost.Web` |
| `Microsoft.AspNet.WebApi.WebHost` | `Rehost.AspNet.WebApi.WebHost` |
| `Microsoft.AspNet.WebApi` | drop, covered by `Rehost.AspNet.WebApi.WebHost` |
| `Microsoft.AspNet.WebApi.Core`, `Microsoft.AspNet.WebApi.Client` | drop, covered by `Rehost.AspNet.WebApi.WebHost` |
| `Microsoft.AspNet.WebPages` | `Rehost.AspNet.WebPages` |
| `Microsoft.AspNet.Razor` | drop, covered by `Rehost.AspNet.WebPages` |
| `Microsoft.AspNet.Providers.Core` | drop when only `<sessionState customProvider>` names it |

A covered line may stay if its version is the one the Rehost package asks for
or newer. An older one is a package downgrade, and restore fails with `NU1605`.
`Rehost.AspNet.WebPages` needs `Microsoft.AspNet.Razor` 3.3.0, so a 3.2.3 line
beside it fails. Four drops have a reason of their own:

- `Microsoft.AspNet.WebApi` is a meta package that pulls
  `Microsoft.AspNet.WebApi.WebHost` back in beside the Rehost host.
- `Microsoft.Web.Infrastructure` gives CS0433 wherever application code uses
  its types, because `Rehost.Web` carries `Rehost.Web.Infrastructure` with the
  same API.
- `AspNet.ScriptManager.jQuery` and `.bootstrap` register script names only.
  Register the same names in a `PreApplicationStartCode.cs` in the App project.
- `Microsoft.AspNet.Providers.Core` is inert while `mode="InProc"` never
  resolves the provider `<sessionState customProvider>` names.

Pure managed packages stay as they are, often at a newer version than the
legacy line: Entity Framework 6.3+, the other Katana `Microsoft.Owin.*` packages,
ASP.NET Identity 2.2, the other `Microsoft.AspNet.WebApi.*` packages (`Cors`,
`Tracing`, `Owin`), Newtonsoft.Json, Autofac and log4net. Packages that ship
only a `net45` build restore under `NU1701`, which the template silences.

The worked table with every decision is in
[Wingtip Toys, packages.config to PackageReference](../apps/WingtipToys/README.md);
the Identity template's shorter one is in
[WebFormsIdentityApplication](../apps/WebFormsIdentityApplication/README.md).

## Configuration

The legacy `Web.config` is never edited. The build applies
`<Host>/Web.Rehost.config`, an XDT transform, and writes the result to
`rehost_root/web.config`; publish applies `Web.$(Configuration).config` first.
A `Web.Rehost.config` beside the Host replaces the package default wholesale, so
keep its three adjustments (remove `<runtime>`, remove `<system.codedom>`,
rewrite the Optimization controls assembly) and add the application's own
below them. Typical additions:

- connection strings for the environment (LocalDb is Windows-only; a named
  catalog on a server or a container replaces `AttachDbFilename`);
- removal of `<modules>` and `<handlers>` rows whose assembly cannot load here
  (Elmah, Application Insights, `Microsoft.AspNet.SessionState`), and of the
  `<location>` blocks that only registered them;
- an explicit `<machineKey>` where the cookie must survive process
  replacement (see Machine keys below);
- the Rehost assembly names wherever configuration names a Web Pages assembly:
  the `system.web.webPages.razor` section group and its sections, a `.cshtml`
  build provider, or `<compilation><assemblies>`. `System.Web.WebPages`,
  `System.Web.WebPages.Razor` and `System.Web.WebPages.Deployment` become
  `Rehost.Web.WebPages`, `Rehost.Web.WebPages.Razor` and
  `Rehost.Web.WebPages.Deployment`, with no version or public key token. The
  runtime does not remap the original names, and the assemblies they name do
  not exist here.

Examples: [Wingtip Toys](../apps/WingtipToys/README.md#webconfig),
[eShopLegacyWebForms](../apps/eShopLegacyWebForms/README.md),
[YAF](../apps/YAF/README.md#webconfig). Settings the runtime refuses fail
activation with a message naming the file and the entry; the list is in
[compatibility](compatibility.md).

## Preserved source

`RehostAppContentRoot` compiles every `*.cs` under the legacy folder, as the WAP
csproj did, and leaves the markup for run-time compilation. Two differences
from the csproj's explicit file list:

- files on disk that the legacy project never listed are compiled too; name
  them in `RehostAppContentExcludes` (YAF has four orphaned code-behind files);
- `App_Code`, `App_Data`, `App_GlobalResources` and `App_LocalResources` are
  removed from the compile, because the runtime compiles them itself. A WAP
  that kept an `App_Code` folder by accident compiled it into the assembly;
  here the same types would exist twice (CS0433).

A Web Site project (no csproj, `CodeFile=` pages, code in `App_Code`) runs, but
the hosting targets stage a WAP: the `*.cs` sources are excluded from the copy,
and a Host-local target has to copy them. That is the state recorded in
[AjaxControlToolkitSampleSite](../apps/AjaxControlToolkitSampleSite/README.md)
and the open
[project models](follow-ups/web-site-vs-wap-project-models.md) follow-up; the
template does not write a Web Site shape yet.

## Libraries bound to System.Web

A third-party binary that references Microsoft's `System.Web` fails at load
with a `FileNotFoundException` for `System.Web, Version=4.0.0.0,
PublicKeyToken=b03f5f7f11d50a3a`, or later with a type that cannot be cast.
There is no redirect to fix this; the assembly needs recompiling against this
runtime. The order of work that has held for six applications:

1. Probe the package as shipped from a throwaway .NET 10 console project,
   driving the code paths the application invokes, not only the reference.
   A package proven in one application can still fail in the next through a
   path the first never called.
2. Recompile only proven blockers, verbatim, with a provenance record
   (`docs/provenance/`). Katana's `Microsoft.Owin.Host.SystemWeb`, Web API's
   `Microsoft.AspNet.WebApi.WebHost` and `Microsoft.AspNet.WebPages` are the
   three shipped as `Rehost.Owin.Host.SystemWeb`,
   `Rehost.AspNet.WebApi.WebHost` and `Rehost.AspNet.WebPages`; the AJAX
   Control Toolkit, Autofac's Web integration and YAF's fourteen projects are
   rebuilt as source under `apps/`.
3. Expect a Framework facade now and then: Katana's Google provider constructs
   `System.Net.Http.WebRequest.WebRequestHandler`, a type .NET 10 does not
   carry, and a 15-line stand-in assembly closes it
   ([Wingtip Toys](../apps/WingtipToys/README.md#the-one-unanticipated-blocker-systemnethttpwebrequest)).

## Custom build steps

Inventory `PostBuildEvent`, `BeforeBuild`/`AfterBuild` and custom targets in
every legacy project file before the first build. They ran invisibly on
Framework machines, and their output can be load-bearing and absent from source
control. The AJAX Control Toolkit sample site's static resources exist only as
the output of a Windows-only hard-link step; the port serves the same assets
embedded instead
([AjaxControlToolkitSampleSite](../apps/AjaxControlToolkitSampleSite/README.md)).
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
  ([YAF](../apps/YAF/README.md#language-version)).
- `Nullable` and `ImplicitUsings` stay off in the App project, and
  `GenerateAssemblyInfo` is false because the tree has its own
  `AssemblyInfo.cs`. The template sets all three.

## Startup troubleshooting

The first run usually fails in one of four places. The console log names each:

1. **Restore or compile.** A missing type from `Microsoft.AspNet.*` means the
   original package was added instead of the Rehost one. CS0433 means a folder
   the runtime compiles itself was also compiled into the assembly. CS0246 in
   a file the legacy project never listed means an exclude is missing.
2. **Configuration.** A `web.config` entry the runtime refuses fails
   activation with the file and the entry in the message; a `web.config` it
   cannot parse answers every request with ASP.NET's Configuration Error page
   and ends the process with exit code 82. Fix the entry in
   `Web.Rehost.config`, not in `rehost_root/web.config`, which the next build
   overwrites.
3. **`Application_Start`.** A throw there latches: every request answers 500
   for ten seconds, then the process is replaced and `Application_Start`
   runs again, as integrated IIS did. The exception is in the log once.
4. **First request.** A `FileNotFoundException` for a Framework assembly
   (`System.Web`, `System.Net.Http.WebRequest`, `System.Configuration` facades)
   points at a library from the section above. A 404 for a page that exists
   usually means the URL is served by a handler or a rewrite rule the port
   does not honor; [compatibility](compatibility.md) lists them.

The two lines `Rehost physical root path` and `Rehost compilation temp path` at
the top of the log tell where the site was staged and where generated
assemblies go; `AppContext.BaseDirectory` is the site root, as on Framework
(see The base directory below).

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
- `,IsolateApps` / `,IsolateByAppId` suffixes never interoperate across
  runtimes; mixed Framework/port farms must use bare keys
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
