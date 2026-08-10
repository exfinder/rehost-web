# Assembly graph

Framework's own graph is mutually circular. Read from the 4.8.1 reference
assemblies, `System.Web` names `System.Web.Services` among its 22 references and
`System.Web.Services` names `System.Web` among its 9. Circular assembly
references are legal in CLR metadata, which resolves lazily at type load;
Microsoft's build compiled each assembly against the previously built binary
rather than from one dependency-ordered graph, and neither assembly's project
file was published. MSBuild cannot express that shape at all.

Every satellite therefore depends on `Rehost.WebForms.Runtime` and none is
depended upon by it, which is the direction Framework's layering actually ran.
The port first carried the `System.Web.Services` edge inverted, with a
config-only leaf supplying `<webServices>`. That compiled, and it silently made
the ASMX surface unreachable: `WebService` exposes `Context`, `Application`,
`Session`, and `Server`, so the assembly holding it has to see `System.Web`.

`WebServicesSection` is the one type the old direction existed for, and it now
lives in the runtime under `Compatibility/WebServices`, keeping its
`System.Web.Services.Configuration` namespace. Only the containing assembly
moved, which the compatibility contract does not claim. The runtime is its sole
owner: an ASMX slice importing Reference Source must leave
`System/Web/Services/Configuration/WebServicesSection.cs` out of its closure, or
the two definitions collide.

Naming an assembly in the root configuration's `<compilation><assemblies>` is a
deployment promise, because a named entry is a hard `Assembly.Load` and a
missing one fails every request during first-request initialization rather than
where the type is used. Assemblies an application deploys to `bin` are already
covered by the trailing `<add assembly="*" />`, which is how
`Rehost.WebForms.Extensions` is reached today; a named entry is warranted only
where the assembly must resolve regardless of what the application referenced.
