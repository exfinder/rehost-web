# Generated resource contracts

## Decision

Restore only the two missing contracts blocking the runtime build:

- define `SYSTEM_WEB`, matching the original shared `LocalizableString.cs`
  System.Web build branch;
- compile an internal `System.Resources.Tools.StronglyTypedResourceBuilder`
  compatibility copy outside imported Reference Source.

The implementation is checked-in static source. A generator was rejected as
unnecessary one-time complexity. No imported source, package, WindowsDesktop
dependency, or platform-specific branch changed.

## Provenance

| Input | Identity |
| --- | --- |
| Microsoft implementation source | `dotnet/msbuild` commit `39950c6284e1fe6e68890e3655ea7704b42e6a32`, `src/Tasks/system.design/stronglytypedresourcebuilder.cs`, MIT |
| Source lineage statement | MSBuild says extracted from System.Design and almost unchanged except visibility/namespace |
| .NET Framework oracle | user-supplied `System.Design.dll`, SHA-256 `c9387539d4538d24ef8b49e970e4147ad60fade4bd355faaba5085fbf77b52d6` |
| Oracle inspection | ILSpy/ICSharpCode.Decompiler `10.1.1.8388` |
| Rehost adapted source | `src/Rehost.WebForms.Runtime/Compatibility/Resources/StronglyTypedResourceBuilder.cs`, SHA-256 `e93897560483317a4518f3d7ea7a5222660336d3e281678a92b7e70e29d8a30e` |

The POC copied Mono's implementation and defined `SYSTEM_WEB`. Rehost retains
the valid constant discovery but replaces the Mono copy with Microsoft source
and a Framework binary oracle.

## Adaptations and deviations

- Restored original `System.Resources.Tools` namespace; kept type internal so
  System.Web does not gain an unintended public API.
- Retained only the `IDictionary` overload used by
  `BaseResourcesBuildProvider`; direct `.resx` overloads require Windows Forms
  `ResXDataNode`/`ResXResourceReader` and are not needed by this call path.
- Materialized values preserve the original non-`ResXDataNode` path.
- Restored Framework generator marker `4.0.0.0`, neutral English comments,
  identifier cleanup, collision handling, ordering, generated resource-manager
  base name, and CodeDOM member shapes.
- `ITargetAwareCodeDomProvider` handling is omitted: no imported System.Web
  source implements or references that System.Design interface. Add only if a
  future provider compatibility layer demonstrates need.

These deviations are internal and cross-platform. They do not change resource
binary serialization; existing deterministic `System.Web.resources` generation
remains byte-identical to its pinned POC ResGen oracle.

## Validation

- Mandated runtime build: 30 errors to 28; only
  `System.ComponentModel.DataAnnotations.Resources` and
  `System.Resources.Tools` diagnostics removed; 1,083 warnings unchanged.
- xUnit v3 + Shouldly: 2 tests passed. Coverage includes Framework identifier
  normalization, reserved/design-time names, collision rejection, public vs.
  internal CodeDOM shape, resource-manager identity, namespace, and generator
  version.
- Imported `src/System.Web.ReferenceSource` remains unchanged.

## TODO

- Run the focused generated-C# fixture directly on .NET Framework 4.8 when a
  Windows oracle runner exists; record byte hashes for C# and Visual Basic
  providers.
- Revisit omitted `ResXDataNode` and target-aware-provider paths only when a
  supported runtime provider reaches them.
- Replace the file-local nullable disable with annotations when Runtime adopts
  nullable analysis; it currently preserves the legacy source's null-oblivious
  contract.
