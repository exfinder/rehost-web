# Compatibility feature map

Status: open. Priority: high. Depends on explicit support decisions.

## Goal

Maintain a precise user-facing map of source/API and behavioral compatibility.
For each feature record: supported, partially supported, unsupported, or
unassessed; platforms; security/trust constraints; failure mode; and owning
contract/test.

## Rules

- “Supported” means portable tested behavior.
- Nothing is supported only on Windows.
- Partial support names the exact boundary.
- Unsupported behavior fails explicitly where reachable.
- Unassessed is not equivalent to unsupported or supported.
- Story completion updates the map; history stays in Git.

Implemented behavior remains canonical in feature contracts until this aggregate
map is complete.

## Compilation substrate

Supported means portable tested behavior on macOS `arm64` and Windows `x64`,
covered by `CodegenSubstrateTests`.

| Feature | State | Boundary |
| --- | --- | --- |
| `[PreApplicationStartMethod]` in `bin` assemblies | Supported | Runs before any generation, once per process |
| `App_Code` (C#) | Supported | Includes the static `AppInitialize` entry point |
| `<codeSubDirectories>` | Supported | Each named directory compiles into its own assembly first; the main assembly may reference it. Framework's mixed-language motivation does not apply, since Visual Basic is unsupported |
| `App_GlobalResources` | Supported | Neutral resx plus culture satellites, reached through the generated strongly typed class |
| `Global.asax` | Supported | Inline `<script runat="server">`; `Application_Start` and request events |
| Reuse across restart | Supported | An unchanged application restarts without recompiling; an edited one recompiles |
| Two processes, one codegen segment | Supported | Serialized by the cross-process compilation mutex |
| Compile error in top-level code | Supported | Activation completes; every request renders the compilation error page with diagnostics, as Framework does. Recovery is a process restart, since file-change notification is disabled |
| `App_WebReferences`, `.wsdl` | Unsupported | Fails naming the limitation |
| `App_Browsers` | Unsupported | Fails naming the limitation |
| Visual Basic | Unsupported | Registered provider fails naming the limitation and the fix |
| `.aspx`, `.ascx`, `.master`, `.ashx`, `.asmx` | Unassessed | Providers registered, no slice compiles them yet |

## Shipped root configuration

The portable root web configuration is derived from the pinned .NET Framework
4.8.1 baseline. Two collections omit entries whose types this port does not
carry. A build provider resolves its type only when a file of that extension is
compiled, so a registered extension states nothing about whether the slice that
compiles it exists yet.

| Section | Omitted | Reason | Effect |
| --- | --- | --- | --- |
| `compilation/buildProviders` | `.edmx`, `.xoml`, `.svc`, `.xamlx` | Types live in `System.Data.Entity.Design`, `System.WorkflowServices`, `System.ServiceModel.Activation`, and `System.Xaml.Hosting` | Unsupported. Files of these types are ignored rather than reported against a Framework assembly that will never exist here |
| `pages/namespaces` | `System.Web.DynamicData` | Assembly absent | Generated code does not import it. Registering it would fail every compilation, not only code that uses it |
| `pages/controls` | all | Prefix registrations for markup | Deferred to the slice that compiles markup; the standard `asp:` prefix comes from assembly attributes, not this section |

`.wsdl` and `.xsd` stay registered under the type names Framework used, which
resolve here to the port's explicit refusal and its compatibility provider.

## Done when

The first runnable request and every encountered IIS/Windows feature have
entries suitable for a future README compatibility section.
