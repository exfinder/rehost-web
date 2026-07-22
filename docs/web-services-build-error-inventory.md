# Web Services first iteration

**Date:** 2026-07-20
**Result:** runtime build reduced from 34 to 30 errors; warnings unchanged at 1,083. Sibling and tests build with zero errors and warnings.

## Supported now

- Separate `Rehost.WebForms.WebServices.dll` sibling boundary.
- `WebServicesSection`, `ProtocolElement`, `ProtocolElementCollection`, and `WebServiceProtocols`.
- Configuration deserialization and enabled-protocol aggregation.

This is only the surface currently required by `System.Web`. It is not an ASMX/SOAP compatibility claim.
The sibling is intentionally API-incomplete and is not yet a general replacement
for the .NET Framework `System.Web.Services` assembly.

## Explicitly unsupported

- `.wsdl` build-provider proxy generation.
- `Application_WebReferences` discovery-map loading and proxy generation.
- General ASMX hosting, SOAP serialization, discovery, client generation, and protocol behavior.
- Classic implicit protocol defaults. An empty section currently enables no
  protocols; .NET Framework normally adds SOAP, SOAP 1.2, localhost POST, and
  documentation protocols during section initialization.

The original provider sources remain in the pinned import but are excluded from compilation. Same-named internal replacements throw an actionable `PlatformNotSupportedException`, directing applications to separately generated and compiled clients. This improves the POC, which removed the providers and later failed with an empty exception.

## Decisions and tradeoffs

- Imported the complete pinned Microsoft Reference Source directory byte-for-byte. Product compilation uses an explicit allowlist, preserving provenance and a future expansion path without silently claiming unsupported surface.
- Evaluated Microsoft's `System.Web.Services.Description` package. It supplies WSDL object-model types, but not the classic importer, discovery-map, build-provider, or configuration behavior; unnecessary for this iteration.
- Reference Source `ServiceDescriptionImporter` depends on XML serializer importer/exporter internals absent from modern .NET. Porting it now creates a large SOAP subsystem rather than removing four build errors.
- Mono's implementation follows the same importer design and relies on runtime internals available in Mono; it does not provide a portable `net10.0` shortcut.
- Classic .NET Framework has a real binary cycle: `System.Web` references `System.Web.Services`, and `System.Web.Services` references `System.Web`. This iteration avoids the cycle. If full ASMX is approved, use a compile-only `System.Web` contract blueprint and verify drift against the runtime assembly.
- Existing pre-generated, separately compiled service clients are unaffected by build-provider removal, subject to their own modern-.NET dependencies.

## Validation

- Configuration fixture loads the real `system.web/webServices` path and verifies `HttpGet | HttpPost`.
- Empty-section fixture locks the intentional no-default-protocol behavior.
- Web Services sibling: zero build errors and warnings.
- Runtime: four Web Services errors removed; no Web Services errors remain.
- Imported directory recursively matches pinned upstream source.
- Dependency graph audited for known vulnerabilities.
- Execution verified on macOS ARM64. Linux and Windows validation remain pending.

For repeatable local diagnostics, runtime builds disable build servers, shared
compilation, node reuse, and parallel MSBuild. Microsoft Testing Platform does
not accept those build-only switches after `dotnet test --project`; run tests
with `dotnet test --project <project> --no-restore`.

## Follow-up TODOs

- Define an explicit ASMX/SOAP compatibility profile before adding protocol behavior.
- If full ASMX proceeds, create the compile-only `System.Web` contract blueprint plus API-drift tests.
- Prototype importer options and compare generated code with .NET Framework fixtures before choosing an implementation.
- Restore remaining `WebServicesSection` configuration surface and classic
  defaults only when an approved profile or consumer requires them.
- Add offline WSDL, `.discomap`, generated-proxy, ASMX-hosting, and cross-OS differential fixtures with their respective feature layers.
- Remove unsupported-provider replacements when equivalent build-provider behavior exists.
