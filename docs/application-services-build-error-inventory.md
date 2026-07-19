# .NET 10 inventory after ApplicationServices restoration

## Scope and decision

This pass restores `System.Web.ApplicationServices` as an authoritative sibling
assembly. All 22 files from pinned Reference Source remain byte-identical. The
SDK project emits `System.Web.ApplicationServices.dll`; `AssemblyRef` is one
checked-in internal type in a compile-time-only BuildInputs assembly. An unsigned friend declaration permits the
unsigned Rehost `System.Web` assembly to use the original internal membership
adapter contract.

The sole compiled overlay is `CustomLoaderHelper`. Modern .NET cannot create
or unload secondary AppDomains, so the approved overlay instantiates custom
loaders in-process. Its original copy is preserved in commit `b1e82ac`; the
current diff shows the complete adaptation. No other Reference Source file is
excluded.

The sibling project explicitly suppresses only its approved legacy warnings:
CAS metadata, formatter serialization, the associated obsolete override,
Windows COM reachability, and the `ref`/`in` advisory. Formatter behavior and
CAS metadata remain in the compiled assembly. The sibling builds with zero
compiler errors and source warnings.

## Diagnostic delta

| Measure | Designer-service pass | ApplicationServices pass | Delta |
| --- | ---: | ---: | ---: |
| Errors | 119 | 38 | **-81** |
| Warnings | 1,083 | 1,083 | 0 |
| All diagnostics | 1,202 | 1,121 | **-81** |
| Files with errors | 44 | 30 | -14 |
| Unique message groups | 103 | 88 | -15 |

All 77 membership/application-services errors disappeared. The static
`IObjectHandle` alias removed four additional remoting errors. A singleton
BuildInputs `AssemblyRef` avoids duplicate friend-visible types, so no warnings
were added. Its constants inline into consumers; the helper DLL is absent from
the sibling output, dependency manifest, and product metadata.

Remaining errors: 16 AppDomain/remoting/serialization, 10 Windows/native/design,
eight missing Web Services/data-protection types, two generated/resource, and
one each configuration and cascade.

Package versions were already centrally pinned: ConfigurationManager and
Security.Permissions 10.0.10. Build: SDK 10.0.302 / MSBuild
18.6.11.33009, macOS arm64. Sibling exit 0; runtime exit
1. Temporary detailed artifacts are not retained.
