# Comparison with the previous POC

## Comparison boundary

The clean build and independent error inventory were completed and committed as
`03a08a2235cedb2e58db70b480288bc0fcdc4f9f` before this comparison began. The
POC was inspected read-only through Git objects; no POC file was changed and no
POC build or test was run.

| Item | Value |
| --- | --- |
| POC repository | `/Users/vm/repos/Portable.System.Web` |
| Compared revision | `c9c908fa24dd8dd116fd613de8ffe584a715b4b1` |
| Revision date | 2026-02-03 10:16:56 +02:00 |
| Commit count through revision | 222 |
| Initial full source import | `d8eb2755f089eaa266d9a65b062464e0a4e07153` |
| POC worktree during review | Clean, `main` at `c9c908f` |

## Source lineage finding

The POC did not record a Reference Source revision or include a top-level
license/provenance record. Its initial `Portable.System.Web` import nevertheless
contains exactly the same 1,920 source/assets blobs and paths as the newly
pinned `microsoft/referencesource` `System.Web` tree. The only additional file
in that POC directory at the import commit was its project file.

Therefore the POC and Rehost start from content-equivalent Microsoft source,
but only Rehost now has a reproducible upstream revision, tree ID, license, and
immutable baseline commit. This makes POC patches technically comparable while
keeping the new provenance chain authoritative.

Between the POC's initial import and `c9c908f`, its core project changed:

| Change kind | Files |
| --- | ---: |
| Modified | 111 |
| Added | 34 |
| Deleted | 5 |
| Total | 150 |
| Line delta | +12,830 / -2,268 |

The five deleted files include three remote-configuration implementations,
`IHtmlString.cs`, and the original assembly-info file. Four additional source
files remain present but are excluded in the project: `regiisutil.cs`,
`WebReferencesBuildProvider.cs`, `WsdlBuildProvider.cs`, and
`XsdBuildProvider.cs`.

## Category-by-category comparison

| Clean-inventory category | POC treatment | What may be reused | What must not be accepted without review |
| --- | --- | --- | --- |
| Source/preprocessing/resources | Added `SR.cs`, embedded resources, extracted `ModName`, cache `.cspp` outputs, regex output, `AssemblyRef`, and `ThisAssembly` | Generated-output discovery, file list, cache/regex extraction history, resource tests | POC-specific resource base name, `Portable.System.Web` assembly names, hard-coded `ThisAssembly` version, and copied outputs without a reproducible generator |
| Configuration | Added ConfigurationManager package and compile constants; added SMTP/Web Services compatibility types; deleted remote configuration | Package/reference evidence and locations where modern configuration works | Deleting remote configuration, stubs, IIS/machine hierarchy assumptions, and reload behavior without an ADR/profile decision |
| AppDomain/remoting/CAS/serialization | Added Security.Permissions, `AsyncLocal` CallContext, no-op AppDomain/AppDomainSetup extensions, warning suppression, and global unsafe BinaryFormatter enablement | Call-site catalog and candidate experiments | No-op isolation/policy/shadow-copy APIs, automatic unsafe serialization, or warning suppression as a substitute for behavior/security decisions |
| IIS/Windows/native/design-time | Added Directory Services, drawing, caching, performance-counter packages; minimal Enterprise Services and UI editor types; managed replacements and explicit native failure | Dependency list, native call-site catalog, candidate managed algorithms | Empty UI/COM types, altered memory thresholds, blanket native failure, impersonation removal, and platform exclusions without profiles/tests |
| CodeDOM/runtime compilation | Added CodeDOM/MSBuild/Roslyn dependencies and a working C# compiler path; excluded Web References/WSDL/XSD providers | Compiler deployment research, Roslyn diagnostics mapping, end-to-end proof | Hard-coded C# provider, missing VB/configured-provider behavior, excluded providers, changed compiler arguments/references/resources without conformance tests |
| Other packages/assembly partitions | Added application-services, common, resources, and Enterprise Services projects; replaced SQL client | Evidence for sibling boundaries and dependency ordering | Changed assembly identities/type-forwarding, SQL provider identity changes, Mono-derived resource code without complete provenance, and API-only stubs |
| Removed/inaccessible framework internals | Added an alternative custom-loader helper and host services | Candidate host-boundary design inputs | Direct adoption before the application isolation/hosting contract is decided |
| Cascades | Mostly disappeared after the above changes | Confirmation that the clean inventory correctly identified high-fan-out prerequisites | Individual POC edits labeled only “fix build error” without root-cause/test review |

## Areas where the POC confirms mechanical work

### Generated source and resources

The POC confirms that the missing `SR`, `ModName`, cache classes, and generated
regular expressions are the dominant mechanical first layer. It contains useful
candidate outputs and tests. Rehost should use them to reconstruct and verify a
deterministic generation pipeline, not simply copy the generated files as
unexplained source.

Important identity differences must be corrected from authoritative inputs:

- the POC `SR` loads `Portable.System.Web` and the project embeds
  `Portable.System.Web.resources`;
- POC `AssemblyRef.SystemWeb` is `Portable.System.Web`, while Rehost intends to
  produce a `System.Web` assembly;
- POC `ThisAssembly.Version` is hard-coded to `1.0.0.0`;
- the POC `ModName` constants were extracted from .NET Framework 4.8 rather
  than generated from `Names.cspp` in the build.

These are excellent evidence for expected generated values, but the Rehost
build must make the inputs, macro choices, assembly identity policy, and output
verification explicit.

### Package/reference layer

The POC package list closely matches the compiler's forwarded-assembly report:
ConfigurationManager, CodeDOM, Security.Permissions, drawing, runtime caching,
Directory Services, MSBuild, and SQL client support. This confirms the clean
inventory's dependency attribution.

Rehost should add each coherent dependency layer separately and record whether
it is:

- part of the supported runtime;
- build-only tooling;
- a separately versioned compatibility component;
- Windows/profile-specific; or
- temporary compile scaffolding to be removed.

Package addition is mechanical. The support contract for the APIs exposed by
that package is not.

### Assembly partitioning

The POC demonstrates that application-services types, resource tooling, and a
small Enterprise Services surface can be separated from the core project. The
general direction is useful, but the new project should derive public assembly
boundaries from .NET Framework metadata and the Rehost package/assembly policy.
It should not inherit `Portable.*` identities or type-forwarding decisions.

## Experimental or architectural POC choices

### Runtime compilation

The POC proves that ASPX-generated C# can be compiled with Roslyn. In the core
`AssemblyBuilder`, however, it loads
`Portable.System.Web.DotNetCompilerPlatform`, selects
`CSharpCodeProvider` explicitly, and comments out creation from the configured
provider type. That bypasses configured language/provider selection and is not
a semantics-preserving patch.

The POC also excludes Web References, WSDL, and XSD build providers. Its
compiler-provider migration notes acknowledge that Visual Basic imports from
`web.config` were skipped. Rehost should retain the parser/CodeDOM contracts,
design a compiler service, and validate C#, Visual Basic, custom provider,
compiler-option, resource, reference, error-location, and batch behavior before
replaying any implementation.

### AppDomain, remoting, and synchronization

The POC's `AppDomainExtensions` and `AppDomainSetupExtension` intentionally
return null/default values or do nothing for evidence, policy, shadow-copy,
cache, configuration-file, loader, and application-manager settings.
`DoCallBack` runs inline in the current domain. `CallContext` is mapped to
`AsyncLocal` state.

Those changes compile and can support a vertical slice, but they collapse
isolation boundaries and change context-flow/lifetime semantics. They are
experiments to characterize, not compatibility shims to import. The no-op
compilation mutex and zero-size `SRef` similarly alter concurrency and cache
pressure behavior.

### Serialization and security

The POC enables unsafe BinaryFormatter serialization globally from a module
initializer and suppresses `SYSLIB0011`. It also suppresses CAS, constrained
execution region, formatter, remoting lifetime, and platform warnings across
the repository.

This makes the current warning stream unsuitable as an audited compatibility
ledger. Rehost should keep warnings visible until each is tied to an ADR,
security decision, test, and narrowly scoped suppression if one remains
necessary. Unsafe formatter support must be an explicit host/security profile,
not an unconditional assembly-load side effect.

### Request termination and timeouts

The POC replaces `Thread.Abort`/`ThreadAbortException` behavior with an ordinary
`CancelModuleException`, pending-timeout checkpoints, and `Thread.Interrupt`.
This is a thoughtful candidate design and the README lists important missing
tests. It changes immediacy, exception propagation, automatic rethrow,
`finally`/catch behavior, blocking-call behavior, and page/pipeline unwinding.

Rehost should use the POC commit sequence as a test-case catalog, then decide
the behavior through differential tests for `Response.End`, redirects,
timeouts, `Server.Execute`/`Transfer`, asynchronous pages, modules, and nested
exception handling.

### Native hosting and cryptography

The POC makes `UnsafeNativeMethods` fail from its static constructor on .NET 10
and redirects selected call sites to managed implementations. This provides a
useful native dependency map and several candidate replacements, but a static
constructor failure affects every remaining native member as one unit.

Managed machine-key/hash replacements require byte-for-byte vectors against
.NET Framework. The POC currently generates autogen keys in memory for every
process start, so protected data does not survive restarts. Its container-aware
memory monitoring also intentionally changes pressure thresholds. These are
behavioral and security decisions, not compile fixes.

### Configuration and unsupported features

The POC deletes remote configuration, supplies minimal SMTP/Web Services and
design types, and excludes several build providers. These may become legitimate
unsupported-profile decisions, but only after the compatibility matrix defines
where failure occurs and what diagnostic is presented. Silent no-ops and empty
types should not be the default.

## Recommended POC replay policy

Classify every candidate commit before replay:

1. **Mechanical and reproducible:** package reference, SDK metadata, generated
   output pipeline, or assembly partition with no behavioral change. Recreate
   cleanly and verify against the POC/output; do not cherry-pick blindly.
2. **Semantics-preserving candidate:** managed implementation intended to match
   the original. Require focused differential tests before adoption.
3. **Intentional deviation:** changed behavior such as unsupported remote
   configuration, platform profile, serialization policy, or timeout model.
   Require an ADR, compatibility-matrix entry, and actionable failure mode.
4. **Experimental shortcut:** hard-coded C# provider, broad suppression,
   unconditional unsafe switch, empty/no-op compatibility type, or unexplained
   source exclusion. Do not replay; redesign after the relevant decision.

The POC commit history is particularly valuable because its early sequence
tracks the clean diagnostic categories almost one for one. That history should
be converted into a candidate-patch ledger with links to the clean category,
affected public behavior, required tests, security/platform impact, and replay
classification.

## Final resolution order after comparison

The POC evidence reinforces, rather than changes, the order from the clean
inventory:

1. reproduce generated resources/preprocessed source with authoritative
   identities;
2. add foundational package/build references in isolated layers;
3. restore correct sibling assembly partitions;
4. define runtime/design-time/Windows/cross-platform profiles;
5. characterize configuration and compiler-provider behavior;
6. decide isolation, remoting, request termination, and application lifetime;
7. decide serialization, CAS, authentication, cryptography, and key persistence;
8. implement host-neutral native/IIS adapters and explicit unsupported paths;
9. rerun a clean build inventory before accepting semantic POC candidates.

This ordering gets the benefit of the POC's discoveries without inheriting its
experimental contracts or obscuring compatibility debt.
