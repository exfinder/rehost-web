# Web Services compatibility

## Implemented surface

`Rehost.WebForms.WebServices` compiles the full `System.Web.Services`
reference source minus the exclusions below: the ASMX server runtime (SOAP
1.1/1.2 doc/literal and rpc/encoded serving, SOAP headers and extensions,
session-enabled and one-way methods, HTTP GET/POST bindings including the
localhost-only POST default), the client runtime that committed generated
proxies (`SoapHttpClientProtocol` family) compile against, discovery
(`?disco`, `.disco`/`.discomap` clients, physical dynamic discovery), the
documentation protocols (literal `?wsdl`, the help page from the vendored
`DefaultWsdlHelpGenerator.aspx` beside machine.config), WS-I Basic Profile
conformance checking, and the full `<webServices>` configuration section with
Framework's protocol defaults (`HttpSoap`, `HttpSoap12`, `HttpPostLocalhost`,
`Documentation` when configuration says nothing).

The baseline root configuration maps `*.asmx` through Framework's chain:
`ScriptHandlerFactory` (in `Rehost.WebForms.Extensions`) answers JSON
script-service calls (`[ScriptService]`, `/js` proxy scripts) and hands
everything else to `WebServiceHandlerFactory`. Both satellite assemblies are
named in `<compilation><assemblies>` as Framework named their originals; the
`Rehost.WebForms` metapackage guarantees they are deployed.

Scenario evidence: `tests/Rehost.WebForms.Hosting.Tests/AsmxOverKestrelTests.cs`
over the shared page fixture (`Calc.asmx`, `ScriptCalc.asmx`).

## Wire readings vs full IIS (2026-08-21)

The same two services served by IIS 10 / .NET Framework 4.8 on the Windows
validation host and by the port, nine responses diffed byte-for-byte with
volatile headers (`Date`, `Server`, `X-AspNet-Version`, `X-Powered-By`) and
host:port normalized:

- **Byte-identical**: SOAP 1.1 invoke, SOAP 1.2 invoke, JSON script-service
  invoke, help page HTML. The `/js` proxy script differs only in its
  generation-time `Expires`/`Last-Modified` stamps.
- **`Environment.NewLine`**: `XmlTextWriter` emits CRLF after the XML
  declaration on Windows and LF off it (HTTP-POST response, `?disco`,
  `?wsdl`). Byte-identity holds per OS, not across them.
- **Serializer emission order**: the modern `XmlSerializer` writes `xmlns`
  attributes in a different order than Framework's generated serializers and
  expands `<s:anyAttribute />` to `<s:anyAttribute namespace="##any" />` in
  `?wsdl` schema output. Semantically identical XML.
- **Fault text is runtime-owned**: `faultstring` embeds
  `Exception.ToString()`, where modern .NET breaks the line before
  `--->` and includes reflection invoker frames Framework did not. Fault
  shape (`soap:Fault`, `faultcode soap:Server`, HTTP 500, message text)
  matches.

## Excluded surface

- **WSDL→proxy generation (`.wsdl` build provider, `App_WebReferences`,
  `ServiceDescriptionImporter`)** — blocked on eleven `System.Xml.Serialization`
  code-export APIs cut from modern .NET (`SoapSchemaImporter`,
  `XmlCodeExporter`, …). Entry points throw `PlatformNotSupportedException`
  directing users to dotnet-svcutil or a committed `Reference.cs`. Restoring
  this means vendoring a serialization-stack fork (the dotnet-svcutil
  precedent); recorded in [follow-ups/web-services](follow-ups/web-services.md).
- **`?wsdl` generation for `Use=Encoded` services** — needs the cut
  `SoapSchemaExporter`; throws actionably. Serving encoded requests works.
- **VS debugger causality channel** (`RemoteDebugger`/`Interop`) — Windows COM
  server, replaced by a stub whose gates are permanently closed, which is also
  Framework's behavior when the COM activation fails.
- **IIS-metabase (`.vsdisco`) dynamic discovery** — ADSI; the virtual-search
  path throws. Framework ships no `.vsdisco` handler mapping by default.
- **COM+ web-method transactions** — `[WebMethod(TransactionOption=…)]` other
  than `Disabled` reaches the runtime's `Transactions` shim, which throws
  ([enterprise-services-compatibility](enterprise-services-compatibility.md)).
- **JSON application services** (`*_JSON_AppService.axd`) — WCF-hosted, not
  compiled; the built-in names resolve to no service.

Original Framework-only code is retained behind `#if NETFRAMEWORK`; each
deviation is listed in
[provenance](provenance/system-web-services-reference-source.json).

Implementation:
`src/Rehost.WebForms.WebServices`.
Remaining scope:
[Web Services](follow-ups/web-services.md).
