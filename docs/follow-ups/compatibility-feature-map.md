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

## Supported applications

| Feature | State | Boundary |
| --- | --- | --- |
| `<httpRuntime targetFramework="4.5" />` or later | Required | Every request renders the refusal naming the fix, which is how Framework reports a startup configuration failure. Below 4.5 the quirks switch selects pre-4.5 behavior at some thirty sites, including `machineKey compatibilityMode`, which routes view state through native cryptography this port refuses. The Visual Studio project template has emitted the attribute since 4.5 |
| Native ASP.NET libraries | Unsupported | `UnsafeNativeMethods` refuses on type initialization, so an unported path names the contract rather than reporting a missing `webengine4.dll` (ledger P41) |
| Legacy machine key cryptography | Unsupported | Reached by `machineKey compatibilityMode` below `Framework45`, which the gate above already refuses, and by the obsolete `MachineKey.Encode`/`Decode` on any application. `MachineKey.Protect`/`Unprotect` are unaffected |
| `BinaryFormatter` serialization | Supported | .NET 9 removed the implementation, so the port carries the out-of-band package and turns the runtime switch on for consuming applications. Reached from view state, out-of-process session state, the roles cookie, out-of-process output cache, preserved compilation results, `LosFormatter`, and binary-serialized `.resx` nodes. Deserialization exposure equals .NET Framework's — neither narrowed nor widened (ledger P42, P43) |

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
| `App_Browsers` | Unsupported | Fails naming the limitation. The built-in browser definitions still apply; only application-level `.browser` overrides are refused |
| Visual Basic | Unsupported | Registered provider fails naming the limitation and the fix |
| `.ascx`, `.master`, `.ashx`, `.asmx` | Unassessed | Providers registered, no slice compiles them yet. `.ashx` is mapped to `SimpleHandlerFactory` by the shipped configuration but nothing exercises it |

## Pages

Supported means portable tested behavior on macOS `arm64` and Windows `x64`,
covered by `PageCompilationTests` and `PageOverKestrelTests`.

| Feature | State | Boundary |
| --- | --- | --- |
| `.aspx` GET, parse, compile, render | Supported | Routed by the shipped `httpHandlers` mapping to `PageHandlerFactory`; compiled on first request and reused until the markup changes |
| `CodeFile` code-behind | Supported | The `.aspx.cs` compiles alongside the generated page class as a partial |
| `Label`, `Repeater`, `HyperLink`, `Image`, `Panel` | Supported | Includes `ItemTemplate` compiled to its own builder, `RepeaterItem` naming containers, data binding from code-behind, and `~/` URL resolution |
| Literal markup of 256 characters or more | Supported | Emitted as a metadata string rather than a Win32 resource; rendered bytes unchanged, per-request transcode instead of a byte copy (ledger P39) |
| Request validation | Supported | Framework's default; a query value containing `<` is refused before the page runs |
| `<form runat="server">`, postback, view state | Supported | Covered under request bodies below; `__VIEWSTATEGENERATOR` carries a permanent divergence recorded in [machine key and view state](machine-key-and-viewstate-bootstrap.md) |
| Controls requiring a server form | Partially assessed | `TextBox`, `Button`, `LinkButton`, and `FileUpload` render and post back. Other controls calling `VerifyRenderingInServerForm`, `GridView` among them, are unassessed rather than blocked |

## Request bodies

| Feature | State | Boundary |
| --- | --- | --- |
| Raw request bodies | Supported | Real Kestrel HTTP/1.1 fixed-length and delayed chunked bodies pass on macOS `arm64` and Windows `x64` through `InputStream`, `BinaryRead`, buffered input, bufferless sync/APM, and enabled async preload. The Framework deterministic matrix, `Expect: 100-continue`, mid-read abort, and unread-body drain are covered. HTTP/2 and HTTP/3 are slice 6 gates |
| Forms, postback, view state, multipart uploads | Supported | Urlencoded and multipart form parsing, view state, control state, event ordering, MAC enforcement, and request validation, covered by `PostbackOverKestrelTests` and `MultipartPostbackOverKestrelTests`. `__VIEWSTATEGENERATOR` carries a permanent divergence (ledger P48) |
| Saving uploaded content to disk | Supported | `HttpPostedFile.SaveAs` and `HttpRequest.SaveAs`, from memory and from the temp file used above `requestLengthDiskThreshold`. `requireRootedSaveAsPath` keeps Framework's default and message. A path rooted only on Windows is refused off Windows, naming the platform rather than reporting "not rooted" or silently writing a file named for the whole path (ledger P50); Windows behavior is unchanged. Whether both hosts classify every header identically is unverified and reaches only the raw-request save |
| `Request.Filter` | Unassessed | Nothing installs an input filter yet |
| `MaxHttpCollectionKeys` rejection | Unassessed | The limit is imported but no test posts past it |
| Request-body size limits | Supported | Kestrel `MaxRequestBodySize` remains host-owned and `httpRuntime.maxRequestLength` remains System.Web-owned; the smaller effective limit wins. The adapter changes neither and rejects no mismatch. Kestrel rejection remains host-owned; System.Web rejection remains pipeline-owned. Known and unknown lengths pass on both supported platforms. Legacy `system.webServer` `maxAllowedContentLength` mapping belongs to its dedicated follow-up |

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
| `pages/controls` | `System.Web.UI`, `...WebControls`, `...WebControls.Expressions`, `System.Web.DynamicData`, `...WebControls` | Types live in `System.Web.Extensions`, `System.Web.DynamicData`, and `System.Web.Entity` | Unsupported. Five of Framework's six entries. The surviving entry registers `System.Web.UI.WebControls.WebParts`; `WebControls` itself needs no entry because `[assembly:TagPrefix]` already registers the `asp:` prefix for it |
| `httpHandlers` | `*_AppService.axd`, `ScriptResource.axd`, `*.asmx`, `*.rem`, `*.soap`, `*.svc`, `*.xoml`, `*.xamlx` | Types live in `System.Web.Extensions`, `System.Runtime.Remoting`, `System.ServiceModel.Activation`, and `System.Xaml.Hosting` | Unsupported. Requests for these extensions fall through to the trailing catch-all instead of being reported against an assembly that will never exist here. Framework marks all eight `validate="false"`, so it too defers them to request time |
| `browserCaps/result` | `System.Web.Mobile.MobileCapabilities` | Type lives in `System.Web.Mobile` | **Substituted**, not omitted, with its own base `System.Web.HttpBrowserCapabilities`. Omitting the element defaults the result to `HttpCapabilitiesBase`, which `HttpRequest.Browser` cannot cast; omitting the whole section leaves `Request.Browser` null and every control reading it throws. Code casting to `MobileCapabilities` cannot exist here because the assembly is absent. `System.Web.Mobile` is otherwise unported |

### Behavior the handler list brings with it

Framework's list is taken whole apart from the rows above, so its trailing
entries come too, and they are not inert.

| Path | Effect here |
| --- | --- |
| `*` → `DefaultHttpHandler` | `HttpWorkerRequest.SupportsExecuteUrl` is false for every worker request in this port, so `DefaultHttpHandler` does not delegate to a host and serves the file itself through `StaticFileHandler`. System.Web therefore serves static files, alongside whatever the ASP.NET Core host serves |
| `*` → `HttpMethodNotAllowedHandler` | Verbs outside `GET,HEAD,POST` get Framework's 405 rather than a 404 |
| `*.cs`, `*.config`, `*.asax`, `*.master`, and the rest of the forbidden set | 403 rather than the 404 these paths returned before the list was shipped. Asserted by `PageOverKestrelTests` |

`.wsdl` and `.xsd` stay registered under the type names Framework used, which
resolve here to the port's explicit refusal and its compatibility provider.

## Done when

The first runnable request and every encountered IIS/Windows feature have
entries suitable for a future README compatibility section.
