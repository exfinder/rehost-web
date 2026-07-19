# Deterministic System.Web build inputs

## Scope

This layer recreates only the original generated and preprocessed inputs needed
to compile the pinned `System.Web` sources. It does not edit the imported tree,
add packages, port runtime behavior, add compatibility types, suppress warnings,
or exclude source files.

The runtime project builds a package-free generator first and writes its outputs
under:

```text
src/Rehost.WebForms.Runtime/obj/<Configuration>/net10.0/generated/System.Web/
```

The generated files are intermediate build artifacts and are not checked into
`src/System.Web.ReferenceSource`. Assembly identity constants instead live in
one checked-in, compile-time-only BuildInputs assembly shared by product
projects.

## Inputs and outputs

| Input | Explicit supplemental input | Output | Verification focus |
| --- | --- | --- | --- |
| `System.Web.txt` | Resource identity in `assembly-identities.json`; pinned POC ResGen output as golden oracle | `SR.g.cs`, `System.Web.resources` | 3,327 unique names, ResGen-compatible whitespace and escape decoding, exact POC binary hash, `System.Web` base/logical name |
| `Names.cspp` | `native-names.json` (`names.h` macros and `FEATURE_PAL=false`) | `ModName.g.cs` | Desktop Framework 4 Windows module/registry constants; no unresolved preprocessor branch |
| `cacheusage.cspp` | None | `CacheUsage.g.cs` | Macro expansion and `UsageEntryRef`, `UsageBucket`, `CacheUsage` symbols |
| `cacheexpires.cspp` | None | `CacheExpires.g.cs` | Macro expansion and `ExpiresEntryRef`, `ExpiresBucket`, `CacheExpires` symbols |
| Microsoft `regcomp/RegexPreCompiler.cs` normalized in `regular-expressions.json` | Pinned external-path provenance | `RegularExpressions.g.cs` | 27 original constructible type names, patterns, declared options, visibility, and timeout constructors; generated constructors additionally request `RegexOptions.Compiled` |
| `assembly-identities.json` | Rehost identity policy and pinned source usages | checked-in `eng/Rehost.WebForms.ReferenceSource.BuildInputs/AssemblyRef.cs` | Exact coverage of every `AssemblyRef.*` use; product assembly versions remain project-local |

Complete machine-readable provenance, upstream blobs, SHA-256 values, and POC
comparison boundaries are recorded in
`docs/provenance/generated-build-inputs.json`.

The POC `System.Web.resources` file is the golden oracle for resource
serialization. It is not copied into this repository: the portable generator
reproduces it byte-for-byte from the pinned `System.Web.txt`, including
ResGen's removal of whitespace immediately following `=`.

The regex manifest retains Microsoft's declared options. Generated constructors
add `RegexOptions.Compiled` when invoking the .NET 10 `Regex` base constructor.
This requests runtime compilation where dynamic code is available and falls
back to the interpreter where it is not. It is an intentional performance
choice and makes the observable `Regex.Options` value differ from the original.

- [ ] **TODO — regex generation:** Revisit build-time `[GeneratedRegex]` or a
  project-owned source generator after assembly/API, culture, and timeout
  compatibility decisions. Preserve the original constructible types and
  callers; benchmark against this `RegexOptions.Compiled` baseline.

## Identity choices represented by this layer

- Output assembly: `System.Web`, version `4.0.0.0`, neutral culture, unsigned.
- Neutral resource: base name `System.Web`, logical name
  `System.Web.resources`.
- `AssemblyRef.SystemWeb`: `System.Web`, never `Portable.System.Web`.
- `AssemblyRef.SystemConfiguration`: original reflection string
  `System.Configuration`; package/reference mapping is deliberately deferred.
- `AssemblyRef.MicrosoftPublicKey`: legacy Microsoft token
  `b03f5f7f11d50a3a`, retained only where original configuration/reflection
  strings require it. It does not describe or sign the Rehost output.
- Native-name target: desktop .NET Framework 4 Windows with `FEATURE_PAL`
  explicitly disabled. Cross-platform native hosting is outside this layer.

The unsigned output cannot satisfy legacy strong-name bindings even though the
source still constructs some legacy Microsoft-qualified strings. Resolving
that distinction requires the later assembly-identity and compatibility-policy
decision.

## Reproduction and verification

Generate explicitly:

```text
dotnet run --project eng/Rehost.WebForms.GeneratedInputs/Rehost.WebForms.GeneratedInputs.csproj -- generate <repository-root> <output-directory>
```

Run the focused verification suite:

```text
dotnet run --project eng/Rehost.WebForms.GeneratedInputs/Rehost.WebForms.GeneratedInputs.csproj -- verify <repository-root>
```

The verification command generates every artifact twice in separate unique
temporary directories and requires byte-for-byte equality. It then validates:

- all resource names and values against `System.Web.txt`;
- byte equality by SHA-256 with the pinned POC ResGen resource output;
- `SR` constant count and resource base name;
- every checked-in `AssemblyRef` constant referenced by the imported source;
- native module and registry constants;
- cache type symbols and complete removal of function macros;
- all 27 regex patterns, declared options plus `RegexOptions.Compiled`,
  visibility values, default constructors, and timeout constructors;
- absence of `Portable.*` assembly and resource identities.

The runtime project imports the generation wiring from
`eng/Rehost.WebForms.GeneratedInputs/Rehost.WebForms.GeneratedInputs.targets`.
MSBuild `Inputs`/`Outputs` tracking skips generation when every artifact exists
and is newer than its pinned input. Cleaning `obj`, changing an input, or
changing the generator runs it before `PrepareForBuild`. `AssemblyRef` is not a
generated artifact: product projects reference the non-packable BuildInputs
project with copy-local disabled, and its constants are inlined. The SDK remains
pinned by `global.json`; neither build-input project has external package
references.

## Recorded verification results

The completed generated-input pass was verified on 2026-07-19 against the
pinned source tree and .NET SDK 10.0.302:

- the generator project built with 0 errors and 0 warnings;
- the focused `verify` command passed, including two-run byte repeatability,
  exact reproduction of the POC ResGen resource SHA-256, and all symbol,
  resource, constant, regex, and identity checks listed above;
- `git diff --check`, JSON parsing, and trailing-whitespace checks passed;
- `git diff --exit-code -- src/System.Web.ReferenceSource` confirmed that the
  imported Reference Source tree was unchanged;
- the integrated `net10.0` build reached compilation and completed its
  diagnostic collection with 3,487 errors and 755 warnings, as recorded in
  `docs/generated-input-build-error-inventory.md` and the associated inventory
  metadata.

The raw diagnostic log and binlog are intentionally untracked. Their paths and
SHA-256 hashes, together with the normalized diagnostic inventory hash, are
recorded in `artifacts/build/net10.0-generated-inputs/build-metadata.json`.
