# Web Services scope

The T1–T3 + script-services port planned here landed 2026-08-21 (see
[web-services-compatibility](../web-services-compatibility.md) for the shipped
profile and [system-web-services-portability](../research/system-web-services-portability.md)
for the analysis that grounded it). What remains is deliberate backlog:

- **T4 — WSDL→proxy generation** (`ServiceDescriptionImporter`, the `.wsdl`
  build provider, `App_WebReferences`, encoded `?wsdl`). Blocked on eleven
  `System.Xml.Serialization` code-export APIs cut from modern .NET. Restoring
  it means vendoring an importer/exporter slice of the serialization stack
  internal to the satellite (dotnet-svcutil's `FrameworkFork` precedent; the
  pinned referencesource carries the sources under the same MIT license). If
  vendored, the BCL serializer stays on the wire, and the frozen-mapping-rules
  assumption (fork importers vs. live runtime serializer) gets recorded in the
  compatibility doc. Typed-DataSet import additionally needs
  `System.Design`'s `TypedDataSetSchemaImporterExtension` — a separate
  decision. Trigger: a real application that ships raw `.wsdl` under
  `App_WebReferences` or needs encoded WSDL generation.
- **Framework config identity remapping** — `system.webServer` module and
  handler rows naming `System.Web.Services, … b03f5f7f11d50a3a` are retargeted
  to this assembly by `IisTypeIdentities`, alongside `System.Web` and
  `System.Web.Extensions`. Every other carrier of an assembly-qualified type
  name — other configuration sections, `.discomap` files — still resolves
  against the Framework identity and fails; decide when a real application
  carries such strings.
- **rpc/encoded fixtures** — serving encoded requests was probe-verified in
  the research; no standing scenario exercises it, and encoded `?wsdl`
  fail-fast has no test.
