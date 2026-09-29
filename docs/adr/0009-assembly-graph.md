# Assembly graph

Framework's own graph is mutually circular. Read from the 4.8.1 reference
assemblies, `System.Web` names `System.Web.Services` among its 22 references and
`System.Web.Services` names `System.Web` among its 9. Circular assembly
references are legal in CLR metadata, which resolves lazily at type load;
Microsoft's build compiled each assembly against the previously built binary
rather than from one dependency-ordered graph, and neither assembly's project
file was published. MSBuild cannot express that shape at all.

Every satellite therefore depends on `Rehost.Web` and none is
depended upon by it, which is the direction Framework's layering actually ran.
The port first carried the `System.Web.Services` edge inverted, with a
config-only leaf supplying `<webServices>`. That compiled, and it silently made
the ASMX surface unreachable: `WebService` exposes `Context`, `Application`,
`Session`, and `Server`, so the assembly holding it has to see `System.Web`.

`WebServicesSection` is the one type the old direction existed for. It first
moved into the runtime under `Compatibility/WebServices` as a minimal
hand-written stand-in, and the runtime was its sole owner while the satellite
stayed config-only. The ASMX port (2026-08) reversed that: the reference-source
section is the assembly's wiring hub — it instantiates the server protocol
factories and exposes `internal` members the protocol machinery consumes — so
it can only compile alongside that machinery. The full section now lives in
`Rehost.Web.Services`, imported unmodified; the runtime carries no
`System.Web.Services.Configuration` types at all. The runtime's single
compile-time need, the typed `SystemWebSectionGroup.WebServices` getter, is
disabled surgically; the root-config `<section>` entry names the satellite and
resolves lazily, so only applications that touch `webServices` configuration
or serve `.asmx` load it — the same promise shape as a `validate="False"`
handler entry.

Naming an assembly in the root configuration is a deployment promise. In
`<compilation><assemblies>` a named entry is a hard `Assembly.Load`, failing
every request during first-request initialization rather than where the type is
used; assemblies an application merely deploys to `bin` are already covered by
the trailing `<add assembly="*" />`. In `<pages><controls>` it is worse, because
prefix resolution loads every assembly registered for a prefix, so one missing
entry fails `<asp:Label>` as surely as the control the entry was added for.

A handler entry is the mild case: `validate="False"`, as Framework uses for
`ScriptResource.axd`, defers type resolution to a request for that path, so a
missing satellite costs that path rather than every activation.

Framework could keep such promises because the GAC guaranteed the assemblies.
This port names `Rehost.Web.Extensions` under `<controls>` so that an
unchanged application parses `<asp:ScriptManager>`, and keeps the promise by
shipping the four assemblies together. The `Rehost.Web` package carries
`Rehost.Web`, `Rehost.Web.ApplicationServices`,
`Rehost.Web.Extensions` and `Rehost.Web.Services` in its `lib/`,
owns the root configuration, and lists their external dependencies. A fifth
component, `Rehost.Web.Infrastructure`, sits in the same `lib/` for a different
reason. No configuration names it, but satellites and application code compile
against it, and every one of them already depends on `Rehost.Web`. The
five remain separate assemblies and projects but are not packages of their own,
and no public package names their project IDs as a dependency (public alpha
contract, GitHub #8). Satellite packages reference the component projects with
`PrivateAssets="all"` and the bundle project for the dependency edge, so their
nuspecs name `Rehost.Web` alone. The consumer targets ship as
`buildTransitive/`, because ordinary `build/` assets do not flow through package
dependencies.

An assembly ported from `aspnet/AspNetWebStack` keeps its upstream name with
`System` replaced by `Rehost`, and its package ID is upstream's with
`Microsoft` replaced by `Rehost`: `System.Web.Http.WebHost` ships as
`Rehost.Web.Http.WebHost` in the `Rehost.AspNet.WebApi.WebHost` package.
Namespaces stay upstream's. The project folder is named after the assembly and
`<PackageId>` carries the package ID. The two are derived separately because
upstream's packages and assemblies do not map one to one:
`Microsoft.AspNet.WebPages` carries `System.Web.WebPages`, `.Razor` and
`.Deployment`, which ship as `Rehost.Web.WebPages`, `.Razor` and `.Deployment`
in `Rehost.AspNet.WebPages`. `.Razor` references `Rehost.Web.WebPages`, so no
component can carry all three, and a pack-only project bundles them the way
`Rehost.Web` bundles its five. The `Rehost.Web.*` family keeps its
names.
