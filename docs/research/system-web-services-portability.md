# `System.Web.Services` portability analysis

Analysis of the imported reference source
(`src/System.Web.Services.ReferenceSource`, pinned
[ec9fa9ae](https://github.com/microsoft/referencesource/tree/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Services),
174 C# files / 43,110 lines + `System.Web.Services.txt`) answering: what is the
maximum portable surface on .NET 10 without native Windows dependencies, which
Framework-only assembly slices it touches, and what the native code is for.
Weighted toward the workloads Framework web developers actually used.

## Result

**The entire assembly compiles unmodified on .NET 10 against
`Rehost.WebForms.Runtime` given a generated `Res` class (424 lines from
`System.Web.Services.txt`) and an 85-line shim file** (experiment below). The
assembly is ~100% managed: its only P/Invoke is one `ole32.dll
CoCreateInstance` behind the Visual Studio debugger-causality channel, and its
only registry/native-adjacent namespaces (`Interop`, one ADSI file) are
debug-only or dead-by-default. The hard portability line does not run between
"managed and native" — it runs between **serving/consuming SOAP at runtime
(portable, verified working on .NET 10)** and **generating proxy source from
WSDL (blocked on eleven `System.Xml.Serialization`/`System.Data.Design` APIs
that were cut from modern .NET)**. That cut line is clean: the missing APIs are
confined to 4 files, all on the WSDL-import path, none reachable from any
server-runtime code.

Maximum portable surface: everything except WSDL→proxy code generation,
rpc/encoded WSDL *generation* (serving rpc/encoded requests works), the
`.vsdisco` IIS-metabase searcher, COM+ web-method transactions, and the VS
debugger channel — roughly 38k of 43k lines with portable semantics, covering
every mainstream ASMX workload.

## Method

Three independent evidence streams, no repo changes (all experiments in the
session scratchpad):

1. **Source sweep** — exhaustive dependency extraction over all 174 files
   (external member usage, CAS, P/Invoke, COM, registry, path math), by
   namespace.
2. **Brute-force compile** — a scratchpad project compiling all 174 files
   against `net10.0` + `Rehost.WebForms.Runtime` +
   `System.Configuration.ConfigurationManager` / `System.Security.Permissions` /
   `System.CodeDom` / `System.DirectoryServices` (10.0.10) with the repo's
   `System.Web` facade-removal target. Iterated to **0 errors** by shimming;
   every shimmed symbol is therefore a *proven-missing* API, and everything
   else is *proven-present*. (Caveat: csc reports declaration-phase errors
   before binding method bodies, so the first error wave understates gaps —
   the green build is the meaningful endpoint.)
3. **Behavioral probes** — runtime experiments on .NET 10 (macOS arm64)
   exercising the exact serializer entry points the ASMX runtime uses.

## Inventory

| Area | Files | Lines | Role |
|---|---|---|---|
| `Protocols` | 68 | 12,464 | ASMX server runtime + client protocol stack |
| `Description` | 42 | 23,023 | WSDL object model, generated WSDL serializer (11,059 lines), server reflectors (`?wsdl`), client importers (proxy gen) |
| `Discovery` | 26 | 3,842 | `.disco`/`.discomap` client + server discovery |
| `Configuration` | 17 | 2,023 | `<webServices>` section (already partially shipped) |
| root + `Diagnostics` + `inc` + misc | 15 | ~1,600 | `WebService`, attributes, WS-I validation lives in `Description`, tracing, resource/config helpers |
| `Interop` | 6 | 153 | COM interfaces for VS debugging (Windows-only, debug-only) |

Within `Description`, the split matters: object model + pre-generated
serializer + server reflectors ≈ 18.4k lines (portable); client importer layer
≈ 4.6k lines (holds every missing-BCL call site).

## Empirical .NET 10 findings

Presence probe (`System.Private.Xml`, .NET 10.0.10):

| API | .NET 10 |
|---|---|
| `XmlReflectionImporter`, `SoapReflectionImporter` | present, public |
| `XmlSchemaImporter`, `SchemaImporter`, `XmlSchemaExporter` | present, public |
| `CodeIdentifier(s)`, `ImportContext`, mapping types, `XmlSerializer.FromMappings` | present, public |
| `XmlSerializationReader`/`Writer` + every protected member the generated WSDL serializer subclasses use | present (verified against the ref pack) |
| `XmlSchemas.Compile(handler, bool)`, `XmlSchemas.IsDataSet` | **public on .NET 10** (were internal on Framework) |
| `SoapSchemaImporter`, `SoapSchemaExporter` | **missing** |
| `XmlCodeExporter`, `SoapCodeExporter`, `CodeExporter` | **missing** |
| `System.Xml.Serialization.Advanced.*` (SchemaImporterExtension) | **missing** (whole namespace) |
| `XmlSchemaImporter` 4-arg ctor, `.Extensions` property | **missing** |
| `XmlMemberMapping.GenerateTypeName(CodeDomProvider)` | **missing** (internal even on Framework — the assembly's only System.Xml friend-API use) |

Behavioral probes — all of these **work** on .NET 10:

- `XmlReflectionImporter.ImportMembersMapping` + `FromMappings` + round-trip —
  the doc/literal request/response path (`SoapReflector.cs:538`).
- `SoapReflectionImporter.ImportMembersMapping(..., writeAccessors: true)` +
  `FromMappings` + serialize/deserialize with
  `encodingStyle="http://schemas.xmlsoap.org/soap/encoding/"`, including
  soap-enc `int[]` arrays — **the rpc/encoded wire path works on .NET 10**
  (`SoapReflector.cs:534`).
- `XmlSchemaExporter.ExportMembersMapping` — the literal `?wsdl` schema path
  (`SoapProtocolReflector.cs:96`).
- `XmlSchemaImporter.ImportTypeMapping` from a compiled `XmlSchemas` — the
  schema half of WSDL import (the missing half is mappings→CodeDom).
- `CSharpCodeProvider.GenerateCodeFromCompileUnit` works;
  `CompileAssemblyFromSource` throws `PlatformNotSupportedException` —
  irrelevant here because **this assembly never compiles code**: zero
  `CompileAssemblyFrom*`/`ICodeCompiler`/`CompilerParameters` call sites. All
  compilation is delegated to ASP.NET (`WebServiceParser.GetCompiledType`,
  `PageParser.GetCompiledPageInstance`) or to `XmlSerializer` internals.

Compile experiment endpoint: green with **only** these shims —

1. `Res` generated from `System.Web.Services.txt` (418 keys; the netfx build
   generates this; `WebServicesInteroperability.cs:603` needs the
   string-keyed `GetString(string)` form at runtime).
2. `System.EnterpriseServices.TransactionOption` (5-value enum) +
   `System.Web.Util.Transactions.InvokeTransacted`/`TransactedCallback` — both
   already exist in `Rehost.WebForms.Runtime` but are `internal`
   (`Compatibility/EnterpriseServices/`); netfx `System.Web` declares
   `Transactions`, `TransactedCallback`, `WorkItem`, `WorkItemCallback`
   **public** (`System.Web.ReferenceSource/Util/Transactions.cs:26,48`,
   `Util/WorkItem.cs:27`), so a staged port needs no `System.Web` internals —
   only a visibility decision in the runtime.
3. The eleven missing serializer/codegen APIs listed above plus
   `System.Data.DataSetSchemaImporterExtension` /
   `System.Data.Design.TypedDataSetSchemaImporterExtension` (netfx
   `System.Design.dll`, no modern successor).
4. `Assembly.Evidence` + `XmlSerializer.FromMappings(mappings, Evidence)` —
   dead at runtime: every call is behind
   `if (AppDomain.CurrentDomain.IsHomogenous)` taking the evidence-free branch,
   and `IsHomogenous` is constant `true` on modern .NET
   (`SoapServerProtocol.cs:116`, `SoapServerMethod.cs:255`,
   `XmlReturnReader.cs:51`).

Everything else — the whole `System.Web` surface consumed, `System.Net` client
stack, `System.Configuration` section types, `CollectionBase` object model,
obsolete-but-present APIs (`Path.InvalidPathChars`, `new Uri(url, true)`,
`Uri.MakeRelative`, `XmlValidatingReader`) — resolved against .NET 10 plus the
existing runtime. Notably `PageParser.GetCompiledPageInstance` and
`WebServiceParser.GetCompiledType` already exist in the runtime's ported
`System.Web` compilation system, and `DefaultWsdlHelpGenerator.aspx` is already
vendored (`third_party/microsoft/framework-config/`).

## The clean cut: runtime vs proxy generation

The missing BCL surface is confined to four files, all WSDL-import:

| File | Missing APIs used | Blocked feature |
|---|---|---|
| `Description/SoapProtocolImporter.cs` (1,180) | SoapSchemaImporter, Xml/SoapCodeExporter, XmlSchemaImporter ctor/Extensions, DataSet extensions, GenerateTypeName | SOAP proxy generation |
| `Description/MimeXmlImporter.cs` (113) | XmlCodeExporter, XmlSchemaImporter ctor/Extensions, DataSet extensions | HTTP-GET/POST proxy generation |
| `Description/WebCodeGenerator.cs` (1 call, line 129) | `XmlMemberMapping.GenerateTypeName` | language-correct type names in generated code |
| `Description/SoapProtocolReflector.cs` (line 48) | SoapSchemaExporter | **server-side `?wsdl` for `Use=Encoded` services only** |

No `*Server*`, client-protocol, discovery, or configuration file touches any
missing API. Serving rpc/encoded requests needs only `SoapReflectionImporter` +
`FromMappings` (probe-verified working); the gap bites only when such a service
must *generate* its WSDL.

These APIs cannot be thinly shimmed: `XmlCodeExporter` and friends consume
`System.Private.Xml`'s internal mapping object model (evidenced by
`GenerateTypeName` being internal even on Framework). Restoring them means
vendoring a serialization-stack fork — exactly what
[dotnet-svcutil did](https://github.com/dotnet/wcf/issues/5792) with its
private `Microsoft.Xml` `FrameworkFork` — and the pinned referencesource
contains the sources
([XmlCodeExporter.cs](https://github.com/microsoft/referencesource/blob/main/System.Xml/System/Xml/Serialization/XmlCodeExporter.cs))
under the same MIT license. That is a large, self-contained decision, not an
increment.

## Windows-coupled code (all of it)

**Native/COM — VS debugger causality channel (delete/stub, zero loss).**
`Protocols/RemoteDebugger.cs` (336 lines) + `Interop/` (6 files) +
`UnsafeNativeMethods.cs`. Implements the Visual Studio "step from client into
server web method" protocol: exchanges an opaque blob in the
`VsDebuggerCausalityData` HTTP header with an in-proc COM notification server
(`CoCreateInstance` of CLSID `{12A5B9F0-...}`) that Visual Studio registers on
Windows. Activates only when a managed debugger is attached *and* `<compilation
debug="true">` *and* the COM activation succeeds; fails permanently closed
otherwise. Genuinely Windows-specific (COM server shipped by VS) and genuinely
obsolete — `Interop/UserThread.cs` stores a pointer in an `int`, broken on
64-bit, evidencing years of disuse. `NativeMethods.cs` (kernel32 imports) is
entirely inside `#if false`. Stubbing the two gate methods to `false` removes
every native dependency in the assembly.

**Registry: none.** Both `using Microsoft.Win32;` directives
(`WebServiceHandler.cs:19`, `WebServiceHandlerFactory.cs:11`) are dead.

**`System.DirectoryServices` — one file.**
`Discovery/DynamicVirtualDiscoSearcher.cs` (~275 lines) walks the IIS 6
metabase over ADSI (`IIS://host/W3SVC`, `ServerBindings`, `IIsWebVirtualDir`)
to enumerate virtual directories for *dynamic* discovery of a site-root
`.vsdisco`. Managed code, but its object of discourse (the metabase) is
Windows/IIS-only, and the pinned 4.8.1 machine.config/web.config contain **no
`*.vsdisco` handler mapping at all** — the feature is dead by default. The two
other `using System.DirectoryServices` directives in Discovery are unused. The
sibling `DynamicPhysicalDiscoSearcher` is pure `DirectoryInfo` and portable.

**`System.EnterpriseServices` — one enum plus one call.** Public API leak:
`WebMethodAttribute.TransactionOption` property/ctors are typed as the
`System.EnterpriseServices.TransactionOption` enum (storage is deliberately
`int` to avoid loading the assembly — `WebMethodAttribute.cs:27`). Runtime
use: `WebServiceHandler.cs:133` calls
`Transactions.InvokeTransacted(callback, option)`, which on Framework enters a
COM+/MSDTC transaction — genuinely Windows (COM+ catalog + DTC). The repo has
already decided this: the runtime's `Transactions` shim throws
`PlatformNotSupportedException`
([enterprise-services-compatibility](../enterprise-services-compatibility.md)),
which is the right boundary; a `System.Transactions.TransactionScope` mapping
exists as a fallback position but changes semantics (no DTC, no
`ContextUtil`). Reached only when a `[WebMethod]` sets a non-`Disabled`
`TransactionOption`.

**CAS.** 78 declarative attributes (inert with the `System.Security.Permissions`
package) and 17 runtime call sites in 7 files: three `Evidence` plumbing sites
(dead, above), three `Demand()`s, one `Assert()`, plus
`PartialTrustHelpers.FailIfInPartialTrustOutsideAspNet` (permanent no-op —
`IsFullyTrusted` is always true).

That is the complete Windows inventory. Everything else in the assembly is
portable managed code.

## Workload weighting

What Framework web developers actually shipped, and where each lands:

| Workload | Prevalence | Verdict on .NET 10 |
|---|---|---|
| `[WebMethod]` doc/literal services (post-1.1 default) | dominant | **Portable** — serializer core probe-verified; handler pipeline is `System.Web` surface the runtime already exposes |
| Consuming services via generated `Reference.cs` proxies (`SoapHttpClientProtocol`) | dominant | **Portable** — existing generated proxies recompile and need only the client runtime; `HttpWebRequest` no-op deltas below |
| `?wsdl` on doc/literal services | very common | **Portable** — `XmlSchemaExporter` present; generated 11k-line WSDL serializer verified member-by-member |
| SOAP headers (auth tokens) and SOAP extensions (logging/crypto/compression) | common | **Portable** — pipeline is pure `System`/`IO`; ordering semantics preserved |
| Session-enabled web methods, `CacheDuration` | common | **Portable** via runtime session/output-cache seams (`IRequiresSessionState` marker, `Response.Cache`) |
| `.asmx` help page + localhost Invoke form | common (dev-time) | **Portable in this repo specifically** — needs `PageParser.GetCompiledPageInstance` (present in runtime) + vendored `DefaultWsdlHelpGenerator.aspx` (present) |
| rpc/encoded services (SOAP-section-5, .NET 1.x style) | minority, legacy | **Serving works** (probe-verified incl. soap-enc arrays); `?wsdl` generation for them needs missing `SoapSchemaExporter` → actionable failure or static WSDL |
| HTTP-GET/POST bindings | rare (off by default since 1.1; defaults: `HttpSoap12, HttpSoap, HttpPostLocalhost, Documentation`) | Portable; `Encoding.Default` method-name recovery hack becomes a no-op |
| Runtime WSDL import (`.wsdl` build provider, `Add Web Reference` regeneration) | build-time tooling, not runtime | **Blocked** (missing BCL); modern answer is pre-generated clients or `dotnet-svcutil` — matches the existing stance in [web-services-compatibility](../web-services-compatibility.md) |
| `.vsdisco` dynamic discovery, `[WebMethod(TransactionOption=…)]`, VS causality debugging | effectively dead | Excluded (Windows-only or dead by default) |

## Maximum portable surface (tiers)

- **T0 — shipped**: configuration surface (`WebServicesSection` et al.), already
  in `Rehost.WebForms.Runtime`. A full assembly port must resolve type
  ownership: the runtime currently compiles
  `Configuration/ProtocolElement.cs` etc. itself, so a real
  `System.Web.Services` assembly would duplicate those FQNs — move or
  type-forward. Note netfx needs no `InternalsVisibleTo` in either direction
  (all consumed `System.Web.Util` types are public), so the "binary cycle"
  reduces to config-type placement plus the runtime's handler-factory
  registration.
- **T1 — server ASMX runtime** (~14k lines: Protocols server side, root,
  Diagnostics, Description model/serializer/reflectors): doc/literal +
  rpc/encoded serving, literal `?wsdl`, `?disco`, help page, SOAP extensions.
  Needs: generated `Res`; CAS strip or package; visibility of the runtime's
  `Transactions`/`TransactionOption` shims; a seekable request body
  (`SoapServerProtocol` asserts `Request.InputStream` seekability for
  request-element routing); `HttpRuntime.Cache` and
  `HttpWorkerRequest.GetStatusDescription` seams (already present in runtime);
  `SoapSchemaExporter` fail-fast for `Use=Encoded` WSDL generation.
- **T2 — client runtime** (~3k lines: `WebClientProtocol` family +
  Discovery client): supports existing generated proxies, `Discover()`,
  `.discomap`. Fully portable; `GetRelativePath` backslash math and
  case-insensitive filename keys need the usual path fixes.
- **T3 — WS-I BasicProfile validation** (817 lines): pure WSDL analysis,
  portable as-is.
- **T4 — WSDL import / proxy generation** (~4.6k lines): excluded. Restoring it
  means vendoring a serialization-stack fork (dotnet-svcutil precedent);
  keep as explicit backlog, direct users to pre-generated clients.
- **Excluded permanently**: `RemoteDebugger`/`Interop`/`UnsafeNativeMethods`,
  `DynamicVirtualDiscoSearcher`, COM+ transactions (PNSE), CAS enforcement.

## Behavioral deltas on .NET 10 (compile-clean but different)

- `Encoding.Default` is UTF-8, not ANSI: the IIS method-name recovery hack
  (`HttpServerProtocol.cs:167`) becomes a no-op; response-charset fallback
  (`RequestResponse.cs:95`) changes. Legacy charsets need
  `CodePagesEncodingProvider` registration.
- No-op `HttpWebRequest`/`WebRequest` members: `ConnectionGroupName`,
  `PreAuthenticate`, `CachePolicy`, `AllowWriteStreamBuffering`,
  `UnsafeAuthenticatedConnectionSharing`. Functional: credentials, proxy,
  client certs, cookies, decompression, timeouts (sync path).
- Default User-Agent embeds `Environment.Version` (now `10.0.x`).
- `TraceSource`/`BooleanSwitch` no longer initialize from config: the
  `System.Web.Services.Asmx` trace source and all `CompModSwitches` are
  permanently off unless wired programmatically. `Tracing` also calls obsolete
  `Dns.GetHostByAddress` (reverse DNS on the request path) when enabled.
- `ThreadAbortException` catches (~30 sites) and `AppDomain.DomainUnload`
  hooks are dead but harmless.
- `Environment.NewLine` in fault detail and WS-I report text differs off
  Windows (test-baseline concern).
- `Type.GetType` + `Activator.CreateInstance` from config/`.discomap`
  (`TypeElement.cs:51`, `DiscoveryClientProtocol.cs:348`) carry Framework
  assembly-qualified names (`System.Web.Services, ... b03f5f7f11d50a3a`) —
  needs the same identity-remapping treatment as other config type names; the
  whole assembly is reflection/ILGen-driven (no trimming/AOT).
- Latent defects observed (preserve or fix knowingly): per-request `syncRoot`
  in `DiscoveryServerProtocol` does not actually guard the shared cached WSDL
  graph in URI-fixup mode; `LogicalMethodInfo` shares a static non-thread-safe
  `SHA256`; `Debug.Flush()` sits on the request hot path;
  `WebServicesSection.DiscoveryDocumentSerializer` builds and then discards
  the `discoveryReferenceTypes` overrides (extensibility silently inert).

## Prior art

- [dotnet-svcutil](https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-svcutil-guide)
  vendors a private `Microsoft.Xml` serialization fork (`FrameworkFork`,
  visible in [dotnet/wcf#5792](https://github.com/dotnet/wcf/issues/5792)) to
  get schema-import + code-export on modern .NET — the existence proof and the
  cost signal for T4.
- Pinned referencesource contains the missing types' sources
  ([System.Xml/.../XmlCodeExporter.cs](https://github.com/microsoft/referencesource/blob/main/System.Xml/System/Xml/Serialization/XmlCodeExporter.cs)),
  MIT, same import pipeline as existing components.
- Mono carried a full managed `System.Web.Services` including importers —
  design evidence per the project's authority order, not an implementation
  source.

## Relationship to current state

[web-services-compatibility](../web-services-compatibility.md) ships T0 only
and states "the full Framework implementation depends on unavailable XML
serializer importer/exporter internals". This analysis narrows that claim: the
unavailable internals gate **only** proxy generation and encoded-WSDL export;
the server and client runtimes sit on serializer surface that is present and
verified working on .NET 10. The open scope decision in
[follow-ups/web-services](../follow-ups/web-services.md) can therefore be
grounded as: T1–T3 are ordinary porting work against existing runtime seams;
T4 is the only piece requiring a new architectural mechanism.
