# Web Services scope

Plan agreed 2026-08-20, grounded in
[system-web-services-portability](../research/system-web-services-portability.md).

## Scope

Port T1 (ASMX server runtime), T2 (client runtime for committed generated
proxies), and T3 (WS-I conformance checker) into
`Rehost.WebForms.WebServices`, plus the `Script/Services` JSON chain
(`ScriptHandlerFactory`, `RestHandler`, ~2.6k lines) into
`Rehost.WebForms.Extensions`. T4 (WSDL→proxy generation) stays excluded: it
needs eleven `System.Xml.Serialization` codegen APIs cut from modern .NET, and
restoring them means vendoring a serialization-stack fork (dotnet-svcutil
precedent) — a separate decision, still backlog.

## Decisions

- Excluded files (`SoapProtocolImporter`, `MimeXmlImporter`,
  `WebCodeGenerator`'s missing call, `RemoteDebugger`/`Interop`,
  `DynamicVirtualDiscoSearcher`) are left out of the compile, not shimmed.
  Reference points get `#if`/`PlatformNotSupportedException`. Encoded-style
  `?wsdl` generation throws actionable PNSE; serving encoded requests works.
- Full 17-file `<webServices>` config section lives in the runtime
  ([ADR-0009](../adr/0009-assembly-graph.md) direction); the satellite excludes
  `Configuration/` from its import closure. Graph stays acyclic:
  Extensions → WebServices → Runtime.
- Runtime's `Transactions`/`WorkItem` shims and the
  `System.EnterpriseServices.TransactionOption` enum go public, matching the
  netfx public shape. No `InternalsVisibleTo`.
- Baseline config migrates the Framework-exact chain: `*.asmx` →
  `ScriptHandlerFactory, Rehost.WebForms.Extensions`, `validate="False"`,
  delegating to `WebServiceHandlerFactory`; the `.asmx` build-provider entry
  swaps from the PNSE stub to the real provider.
- Framework protocol defaults restored: empty config enables `HttpSoap`,
  `HttpSoap12`, `HttpPostLocalhost`, `Documentation`. The current
  "empty = none" stance was a placeholder for the config-only slice.
- Permanently excluded, unchanged from the research: VS debugger COM channel,
  COM+ web-method transactions (PNSE via the runtime shim), ADSI `.vsdisco`
  discovery.

## Mechanics

Generate `Res` from `System.Web.Services.txt`; identity-remap
`System.Web.Services, ... b03f5f7f11d50a3a` config type names to the
satellite; seekable request body seam for `SoapServerProtocol`; ScenarioHost
fixture deployment carries the satellite (no new test host process). Landing
order keeps main green: runtime config expansion → server core → client →
script services → baseline config lines last.

## Done when

Scenario suite covers invoke, SOAP headers, sessions, faults, `?wsdl`, help
page, and a JSON script service; winbox IIS wire readings byte-diff SOAP
1.1/1.2 request/response, `?wsdl`, help page, and fault shapes; 3-OS rounds
pass. Excluded layers remain actionably unsupported
([web-services-compatibility](../web-services-compatibility.md) updated to the
new profile).
