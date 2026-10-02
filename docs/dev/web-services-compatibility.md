# Web Services compatibility

## Implemented surface

`Rehost.Web.Services` compiles the full `System.Web.Services`
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
`ScriptHandlerFactory` (in `Rehost.Web.Extensions`) answers JSON
script-service calls (`[ScriptService]`, `/js` proxy scripts) and hands
everything else to `WebServiceHandlerFactory`. Both satellite assemblies are
named in `<compilation><assemblies>` as Framework named their originals; the
`Rehost.Web` package ships both.

## Wire boundaries

XML output uses the platform newline. Modern XmlSerializer may reorder xmlns
attributes and explicitly emit `namespace="##any"` on anyAttribute; XML semantics
remain equivalent. SOAP faults keep their status, code and message contract;
stack text follows the executing runtime. Generated proxy-script timestamps
follow generation time.

Modern runtime client behavior differs where the BCL does: default encoding is
UTF-8, legacy code pages need explicit registration, default User-Agent contains
the runtime version, and some HttpWebRequest tuning properties are inert. Configure
TraceSource/BooleanSwitch explicitly. Trimming/AOT is outside the reflection and
runtime-code-generation contract.

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

Original Framework-only code is retained behind `#if NETFRAMEWORK`. Imported
source identity and licenses are in [sources](sources.md).

Implementation:
`src/Rehost.Web.Services`.
Remaining scope:
[Web Services](follow-ups/web-services.md).
