# Web Services scope

Current profile is in [Web Services compatibility](../web-services-compatibility.md).
Remaining decisions:

- **WSDL→proxy generation** (`ServiceDescriptionImporter`, the `.wsdl`
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
- **Framework identity remapping outside `web.config`** — staging rewrites
  `System.Web.Services, … b03f5f7f11d50a3a` in every staged `web.config`, and
  the runtime retargets `system.webServer` module and handler rows. Other
  carriers of an assembly-qualified type name, such as `.discomap` files, still
  resolve against the Framework identity and fail; decide when a real
  application carries such strings.
- **rpc/encoded fixtures** — no standing scenario exercises encoded requests, and encoded `?wsdl`
  fail-fast has no test.

- **Shared-state safety** — assess cached-WSDL synchronization, static hash use
  and discovery serializer overrides before widening concurrency claims.
