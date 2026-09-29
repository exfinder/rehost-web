# Deterministic System.Web build inputs

## Contract

`eng/Rehost.Web.GeneratedInputs` recreates generated/preprocessed inputs
required by pinned System.Web Reference Source. Outputs are deterministic,
package-free intermediates under:

```text
src/Rehost.Web/obj/<Configuration>/net10.0/generated/System.Web/
```

| Input | Output |
| --- | --- |
| `System.Web.txt` | `SR.g.cs`, `System.Web.resources` |
| `Names.cspp` + `native-names.json` | `ModName.g.cs` |
| `cacheusage.cspp` | `CacheUsage.g.cs` |
| `cacheexpires.cspp` | `CacheExpires.g.cs` |
| `regular-expressions.json` | `RegularExpressions.g.cs` |
| `assembly-identities.json` | checked-in `AssemblyRef` constants |

Machine-readable source, hash, license, and transformation records:
[`provenance/generated-build-inputs.json`](provenance/generated-build-inputs.json).

## Identity constraints

- Runtime assembly: `Rehost.Web`, unsigned; its version follows the
  package family in `src/Directory.Build.props`.
- Neutral resource: `System.Web.resources`, base name `System.Web`.
- Legacy Microsoft public-key tokens remain only in original
  reflection/configuration strings; they do not sign Rehost output.
- Native-name preprocessing targets the original desktop Framework constants
  with `FEATURE_PAL=false`. Runtime portability is handled separately.
- Generated regex constructors add `RegexOptions.Compiled`; original type
  names, patterns, declared options, visibility, and timeout constructors stay
  intact.

## Verify

```text
dotnet run --project eng/Rehost.Web.GeneratedInputs/Rehost.Web.GeneratedInputs.csproj -- verify <repository-root>
```

MSBuild runs generation automatically when inputs are newer than outputs.
Future regex generation work:
[regex generation](follow-ups/regex-generation.md).
