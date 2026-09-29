# `System.Web.Services` portability analysis

Port-distance analysis of the pinned 174-file / 43,110-line
[source tree](../../src/System.Web.Services.ReferenceSource/), weighted toward deployed ASMX
workloads.

## Result

All sources compiled on .NET 10 against `Rehost.Web` with a generated 424-line
resource class and 85 lines of proven-missing API shims. Runtime SOAP serving/consumption is
portable and probe-verified. WSDL-to-proxy source generation is the clean cut: eleven removed
XML-serialization/Data.Design APIs are confined to four importer files and unreachable from the
server runtime.

The assembly is effectively all managed. Its sole active P/Invoke is `ole32!CoCreateInstance`
for an obsolete Visual Studio debugger channel. Other Windows-only areas—`.vsdisco` IIS-metabase
discovery and COM+ transactions—are isolated or dead by default. The portable surface is roughly
38k of 43k lines and covers mainstream ASMX.

## Method

Evidence combined an exhaustive dependency/native/CAS/path scan; a scratch `net10.0` compile
against Runtime plus ConfigurationManager, Security.Permissions, CodeDom and DirectoryServices;
and macOS arm64 probes of the exact serializer entry points used by ASMX. Iterative shimming
ended at zero errors, so every unshimmed reference was proven present. No repo files changed.

| Area | Files / lines | Port relevance |
|---|---:|---|
| Protocols | 68 / 12,464 | Server and client runtime |
| Description | 42 / 23,023 | WSDL model/serializer, server reflectors, client importers |
| Discovery | 26 / 3,842 | `.disco`/`.discomap` client/server discovery |
| Configuration | 17 / 2,023 | `<webServices>` sections |
| Other + Interop | 21 / ~1,750 | Attributes, diagnostics, resources, VS debugger COM |

Within Description, object model, generated serializer and server reflectors are portable;
about 4.6k importer lines contain the missing APIs.

## Serializer evidence and clean cut

.NET 10 retains reflection importers, literal schema importer/exporter, mapping types,
`XmlSerializer.FromMappings`, generated-reader/writer protected APIs, `XmlSchemas.Compile`, and
`XmlSchemas.IsDataSet`. It lacks `SoapSchemaImporter`/`SoapSchemaExporter`, code exporters,
`System.Xml.Serialization.Advanced`, the four-argument `XmlSchemaImporter` constructor and
extensions, and `XmlMemberMapping.GenerateTypeName(CodeDomProvider)`.

Probes succeeded for:

- doc/literal request-response mapping and round-trip;
- rpc/encoded mapping and round-trip, including SOAP-encoded integer arrays;
- literal `?wsdl` schema export;
- compiled-schema type import;
- CodeDom source emission. CodeDom compilation is unsupported but unused here; ASP.NET or
  XmlSerializer owns compilation.

Missing calls are localized:

| File | Missing surface | Blocked behavior |
|---|---|---|
| `SoapProtocolImporter.cs` | SOAP importer, code exporters, importer extensions, DataSet extensions, type-name generation | SOAP proxy source generation |
| `MimeXmlImporter.cs` | XML code exporter/importer extensions | HTTP GET/POST proxy generation |
| `WebCodeGenerator.cs` | language-correct generated type name | Proxy source generation |
| `SoapProtocolReflector.cs` | `SoapSchemaExporter` | `?wsdl` for rpc/encoded services only |

The final shim set also included generated `Res` (418 keys); already-imported transaction types
whose visibility requires a Runtime decision; and dead Evidence overloads bypassed because
modern AppDomains are homogeneous. These are staging issues, not additional feature cuts.

The missing code exporters consume private XML mapping internals, so thin shims cannot restore
them. Recovery means vendoring a serializer fork, as dotnet-svcutil does, or using pre-generated
clients. The pinned Reference Source contains
[XmlCodeExporter](https://github.com/microsoft/referencesource/blob/main/System.Xml/System/Xml/Serialization/XmlCodeExporter.cs)
under MIT, but that is a separate large import.

## Windows-only and obsolete edges

| Edge | Reach | Decision |
|---|---|---|
| VS debugger causality (`RemoteDebugger`, `Interop`) | Only debugger attached + debug build + successful COM activation | Stub gate false; removes sole P/Invoke |
| Dynamic `.vsdisco` virtual search | IIS 6 ADSI metabase; no default 4.8.1 handler mapping | Exclude `DynamicVirtualDiscoSearcher`; physical discovery stays portable |
| Web-method transactions | Non-disabled `TransactionOption` invokes COM+/MSDTC | Runtime throws actionable `PlatformNotSupportedException` |
| CAS | Declarative attributes plus a few demands/asserts | Inert under full-trust process contract |

No live registry dependency exists. `NativeMethods.cs` kernel32 declarations are under
`#if false`. Everything else is portable managed code.

## Workload boundary

| Workload | Verdict |
|---|---|
| Doc/literal `[WebMethod]` services | Portable; serializer and handler dependencies present |
| Existing generated `SoapHttpClientProtocol` proxies | Portable after recompilation |
| Literal `?wsdl`, SOAP headers/extensions, session/cache, help page, `.disco` | Portable; required Runtime/vendored assets exist |
| rpc/encoded requests | Serving works; dynamic `?wsdl` requires missing `SoapSchemaExporter` or static WSDL |
| HTTP GET/POST bindings | Portable; rare and mostly disabled by default |
| Runtime WSDL import / proxy generation | Excluded; use pre-generated clients or dotnet-svcutil |
| `.vsdisco`, COM+ transactions, VS causality debugging | Excluded Windows/dead features |

## Scope tiers

- **T0 — configuration:** initially in Runtime; a sibling assembly requires moving or
  type-forwarding duplicate configuration types.
- **T1 — server runtime (~14k lines):** doc/literal and rpc/encoded serving, literal WSDL,
  discovery, help page, extensions. Requires generated resources, transaction-shim visibility,
  seekable request input and actionable encoded-WSDL failure.
- **T2 — client runtime (~3k lines):** existing proxies and discovery clients; portable after
  normal path/case fixes.
- **T3 — WS-I validation (817 lines):** portable WSDL analysis.
- **T4 — WSDL import (~4.6k lines):** excluded pending an explicit serializer-fork decision.
- **Permanent exclusions:** debugger COM, IIS-metabase search, COM+ semantics and CAS enforcement.

T1–T3 and script services have landed. Current claims live in
[Web Services compatibility](../web-services-compatibility.md); T4 remains in the
[follow-up](../follow-ups/web-services.md).

## Accepted/known modern-runtime deltas

- `Encoding.Default` is UTF-8, not ANSI; legacy charsets need
  `CodePagesEncodingProvider`.
- Some `HttpWebRequest` tuning properties are no-ops; credentials, proxy, certificates,
  cookies, decompression and sync timeouts remain functional.
- Default User-Agent includes modern `Environment.Version`.
- Configuration no longer initializes `TraceSource`/`BooleanSwitch`; tracing needs explicit
  wiring.
- `ThreadAbortException` catches and AppDomain unload hooks are dead but harmless.
- Newlines differ off Windows; configured Framework assembly-qualified names need Rehost
  identity mapping; reflection/IL generation makes trimming/AOT unsupported.

Observed latent defects—shared cached-WSDL synchronization, a static non-thread-safe SHA256,
hot-path `Debug.Flush`, and discarded discovery serializer overrides—must be preserved or fixed
deliberately, not silently normalized.

## Prior art

[dotnet-svcutil](https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-svcutil-guide)
uses a private XML serialization fork; Mono provides design evidence only under the project’s
authority order. Both confirm that T4 is feasible but materially separate from ASMX runtime
support.
