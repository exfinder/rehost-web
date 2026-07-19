# Rehost WebForms Project Inception

> Working document for a compatibility-oriented port of .NET Framework 4.x `System.Web` and related ASP.NET Web Forms libraries to modern .NET, initially .NET 10 and Kestrel.

**Status:** Active clean re-baseline; generated-input, foundational-reference, and design-metadata layers complete

**Created:** 2026-07-18  
**Updated:** 2026-07-19  
**Provisional umbrella brand:** Rehost  
**Initial product:** Rehost WebForms  
**POC repository:** `/Users/vm/repos/Portable.System.Web`  
**POC revision reviewed:** `c9c908f`  
**Microsoft Reference Source checkout:** `/Users/vm/repos/referencesource`  
**Microsoft Reference Source revision:** `ec9fa9ae770d522a5b5f0607898044b7478574a3`

## 1. Executive summary

The project aims to let existing ASP.NET Web Forms applications move from .NET Framework 4.x to .NET 10 with minimal source changes. It will preserve the application-facing contracts and observable behavior of `System.Web` while replacing dependencies on IIS, the classic CLR hosting model, Windows-native services, and obsolete runtime facilities with modern .NET and ASP.NET Core infrastructure.

This is best treated as a **compatibility platform**, not as a conventional framework rewrite and not as a literal line-by-line port.

An earlier proof of concept established that the central idea is viable:

- Microsoft Reference Source for `System.Web` can be compiled for .NET 10 after suitable portability changes.
- ASPX parsing and runtime compilation can operate using a modern Roslyn-based compiler path.
- `HttpRuntime` and the classic ASP.NET request pipeline can process Web Forms pages and handlers.
- A custom `HttpWorkerRequest` can bridge ASP.NET Core/Kestrel requests into that pipeline.
- Pages, code expressions, server controls, asynchronous handlers, and global resources work in the demonstrated scenarios.

The recommended next step is **not** to continue development directly on the POC and **not** to discard it. A new implementation should be cleanly re-baselined from a pinned Microsoft Reference Source revision, while the POC is retained as an experimental donor, a catalog of discovered problems, and a source of candidate patches.

## 2. Project goal

Create a supported compatibility runtime that allows a useful and explicitly defined population of legacy ASP.NET Web Forms applications to:

1. Recompile for modern .NET with few application-source changes.
2. Continue using ASPX, ASCX, master pages, code-behind, server controls, ViewState, postbacks, and the Web Forms page lifecycle.
3. Run behind ASP.NET Core and Kestrel on supported modern platforms.
4. Migrate incrementally toward native ASP.NET Core architecture when desired.
5. Understand every known compatibility difference through documentation, diagnostics, and an explicit compatibility matrix.

The central architectural statement is:

> Preserve the observable contracts of ASP.NET Web Forms while replacing IIS and .NET Framework internals with modern .NET hosting and runtime mechanisms.

## 3. Compatibility is a set of separate promises

“Near-zero changes” must not remain an undefined slogan. The project needs to distinguish the following forms of compatibility:

| Compatibility dimension | Initial assessment |
| --- | --- |
| Existing ASPX, ASCX, master-page, and code-behind source compiles | Realistic |
| Existing application source compiles against familiar `System.Web` APIs | Realistic, progressively |
| Page lifecycle, control tree, postbacks, ViewState, and rendering behave equivalently | Difficult but measurable |
| Common `web.config`, session, cache, authentication, module, handler, and provider behavior works | Partially realistic |
| Existing third-party controls work after recompilation | Potentially realistic, vendor-dependent |
| Arbitrary precompiled third-party assemblies work without recompilation | Much harder |
| Binary identity is interchangeable with Microsoft's strong-named `System.Web` | Not generally achievable |

Source compatibility and binary compatibility must be reported separately. The Microsoft private strong-name key cannot be reproduced, so opaque assemblies tied to the original `System.Web, Version=4.0.0.0` identity require an explicit strategy and may remain unsupported.

## 4. Proposed compatibility profiles

Support should be published as named, testable profiles rather than as a single all-or-nothing claim.

### 4.1 Core Web Forms

- ASPX, ASCX, and master-page parsing
- C# and Visual Basic runtime compilation
- Page and control lifecycle
- Postback processing and event dispatch
- ViewState, ControlState, and event validation
- Standard HTML and Web controls
- Rendering, themes, resources, and generated client assets

### 4.2 Application runtime

- `HttpContext`, request, response, cookies, files, and server utilities
- `HttpApplication` pipeline
- HTTP modules and handlers
- Configuration hierarchy and common `system.web` sections
- Session state, caching, output caching, and application state
- Routing and common providers

### 4.3 Extended libraries

- `System.Web.Extensions`
- AJAX/UpdatePanel behavior
- Optimization and bundling libraries
- Membership, roles, profiles, and application services
- Additional historically related ASP.NET packages

### 4.4 Platform-specific compatibility

- Windows identity and impersonation
- COM/Enterprise Services dependencies
- Windows-only data or drawing facilities
- IIS-specific integrated-pipeline behavior

### 4.5 Explicitly deferred or unsupported

Features must be listed here when they cannot be provided safely or faithfully. Unsupported behavior should fail clearly with actionable diagnostics rather than silently doing nothing.

## 5. POC assessment

### 5.1 What should be retained

The POC is substantial and useful:

- It contains 222 commits with many small, discoverable changes.
- About 150 files under the main `Portable.System.Web` project differ from the initial Reference Source import.
- It includes separate projects for the ASP.NET Core bridge, compiler provider, application services, resources, infrastructure, optimization, tests, a test host, and a sample application.
- Its migration README records many substitutions and unresolved questions.
- Its project plan captures the intended `Kestrel -> middleware -> HttpWorkerRequest -> HttpRuntime` flow.
- Its existing executable tests completed successfully: 16 executions passed on .NET 10.0.10 ARM64.

The choice to retain Microsoft's original control, page, compilation, configuration, and pipeline implementations wherever possible is aligned with the goal of maximum semantic compatibility.

### 5.2 Why the POC should not become the production baseline unchanged

The POC was optimized for reaching the next working scenario. Several changes therefore mix build repair, architectural replacement, and intentional semantic deviation without a uniform review gate.

Examples discovered during the audit include:

- The runtime compiler path hardcodes a C# provider, bypassing configured language/provider selection. This would compromise Visual Basic and custom CodeDOM-provider compatibility.
- Several AppDomain and Enterprise Services replacements are silent no-ops. These allow compilation but can conceal behavior differences.
- `Thread.Abort` behavior was replaced with ordinary exception and cooperative interruption logic. This affects `Response.End`, redirects, timeouts, `Server.Execute`, and lifecycle unwinding.
- Autogenerated machine keys are not persisted across restarts.
- Native cryptographic helpers were replaced with managed algorithms and need byte-for-byte compatibility tests.
- Windows impersonation, authentication, monitoring, registry, and native hosting behavior is disabled or replaced.
- Broad warning suppression makes it difficult to distinguish audited compatibility debt from accidental debt.
- Several build providers are excluded from compilation.
- The repository lacks a complete top-level licensing and source-provenance record.

The Kestrel bridge is an effective prototype but still has production concerns:

- Per-request `Task.Run` and synchronous I/O can cause thread-pool pressure.
- Request bodies are fully buffered.
- Requests without `Content-Length`, including chunked requests, can be treated as empty.
- Response writing is synchronous and currently buffers response fragments.
- Repeated response headers require correctness testing.
- Physical path mapping needs canonicalization and root-containment enforcement.
- Cancellation and completion semantics need to prevent legacy processing from outliving an ASP.NET Core request incorrectly.
- Hosting initialization is static and process-global.

### 5.3 Test-isolation incident

The existing test fixture resolved the hosted application's physical root to the POC repository root. During the audit test run, `SampleWebFormsApplication` was deleted, apparently through a runtime code-generation or cleanup interaction. The directory was restored from Git and no tracked deletions remained afterward.

This establishes an immediate rule:

> Every compatibility test must copy its specimen application into a unique disposable directory. Runtime compilation, shadow-copy emulation, cleanup, file monitoring, and precompilation must never operate against a source checkout.

The exact deletion path should be reproduced safely and diagnosed before reusing the existing fixture.

## 6. Source-of-truth policy

Maximum semantic compatibility requires a clear authority hierarchy.

| Purpose | Primary authority |
| --- | --- |
| Public API shape | .NET Framework reference assemblies and API metadata |
| Observable behavior | A running, patched .NET Framework 4.8.1 installation |
| Primary implementation | Microsoft Reference Source at a pinned commit |
| Modern BCL implementation patterns | Current official `dotnet/runtime` sources |
| Alternative implementation ideas | Mono source |
| HTTP hosting behavior | ASP.NET Core/Kestrel contracts |

Microsoft Reference Source should remain the starting point because the objective is Microsoft-compatible semantics. Mono is valuable for replacing unavailable Windows, CLR, remoting, resource, or configuration mechanisms, but Mono behavior must not automatically be treated as the compatibility oracle.

Any implementation derived from Mono should:

1. Record its source file, revision, and license.
2. Explain why Microsoft Reference Source could not be retained.
3. Pass differential behavior tests against .NET Framework.
4. Document intentional differences.

## 7. Proposed architecture

```text
Legacy Web Forms application
  |
  +-- System.Web-compatible public API
  |
  +-- Web Forms compatibility runtime
  |     +-- ASPX/ASCX parser and code generation
  |     +-- Roslyn compilation service
  |     +-- Page and control lifecycle
  |     +-- ViewState, postback, and event validation
  |     +-- Rendering and client resources
  |
  +-- Application services
  |     +-- Configuration
  |     +-- Session, cache, and application state
  |     +-- Authentication and authorization
  |     +-- Modules, handlers, and providers
  |
  +-- Host-neutral System.Web runtime boundary
        |
        +-- ASP.NET Core hosting adapter
              +-- Kestrel / ASP.NET Core middleware
```

The compatibility runtime must not directly depend on arbitrary ASP.NET Core services throughout its implementation. Hosting translation should be concentrated behind well-defined adapters, primarily around the `HttpWorkerRequest` contract and lifecycle services.

Likewise, the public `System.Web` API should not merely expose ASP.NET Core objects under old type names. The goal is legacy behavior, not only familiar method signatures.

## 8. Clean re-baseline strategy

### 8.1 Preserve the POC

- Tag or otherwise freeze POC revision `c9c908f`.
- Label it experimental and avoid cleanup that would erase its history.
- Treat each commit as a candidate patch or recorded investigation.
- Extract unresolved notes from its README and project plan into the new backlog.

### 8.2 Establish the clean source baseline

1. Select and record an exact Microsoft Reference Source commit.
2. Import original files without modifications in a dedicated commit.
3. Include the applicable Microsoft license and notices.
4. Add a machine-readable provenance manifest.
5. Generate a baseline API inventory from .NET Framework reference assemblies.

### 8.3 Inventory before fixing

Perform the first .NET 10 build and record every error rather than fixing errors ad hoc. Classify each error as:

- Missing modern package or reference
- Removed API with an equivalent replacement
- Removed API requiring emulation
- Windows/IIS-specific dependency
- Obsolete or intentionally unsupported behavior
- Source-generation/resource issue
- Compiler or build-system incompatibility

Every resolution should link to a tracked issue, test, ADR, or explicitly mechanical transformation.

### 8.4 Replay the POC selectively

Classify candidate POC changes into four groups:

1. **Mechanical:** SDK project conversion, namespaces, generated resources, package references.
2. **Semantics-preserving:** managed replacements expected to produce equivalent results.
3. **Intentional deviations:** behavior that cannot or should not match .NET Framework.
4. **Experimental shortcuts:** changes useful for proving the concept but unsuitable for production.

Mechanical changes can be replayed after review. Semantics-preserving changes require compatibility tests. Intentional deviations require ADRs and compatibility documentation. Experimental shortcuts should normally be redesigned.

### 8.5 Clean re-baseline checkpoint

The first two mechanical layers were completed on 2026-07-19 without modifying
Reference Source, adding compatibility shims, excluding sources, suppressing
warnings, or changing behavior. A subsequent, explicitly approved shim adds one
compile-time-only design-metadata marker outside Reference Source.

| Build checkpoint | Errors | Warnings |
| --- | ---: | ---: |
| Untouched `net10.0` baseline | 5,794 | 755 |
| Deterministic generated inputs | 3,487 | 755 |
| Foundational package/build references | 286 | 1,083 |
| Design-time metadata marker | 122 | 1,083 |
| Designer-service markers | 119 | 1,083 |

All forwarded-assembly errors are removed. The remaining errors are now
concentrated in deliberate later layers: 85 sibling-assembly types, 19
AppDomain/remoting/serialization dependencies, 10 other
Windows/native/design-time dependencies, and five residual
configuration/generated/internal cascades.

This pass established several durable constraints:

- Windows-specific functionality and design-time tooling are unsupported. The
  runtime remains plain `net10.0` and must not acquire a WindowsDesktop
  dependency. Legacy `UITypeEditor` attribute references resolve to one
  internal, non-instantiable metadata marker; designer-only service branches
  resolve to internal compile-time shapes. Both groups remain outside Reference
  Source and were individually approved; they do not grant blanket approval
  for further shims.
- Reference Source executes MSBuild tasks during runtime compilation. MSBuild
  deployment and toolset selection are runtime architecture concerns, not only
  build-time implementation details.
- Public APIs expose concrete `System.Data.SqlClient` command and exception
  types. `Microsoft.Data.SqlClient` can coexist, but cannot transparently
  replace those contracts; any dual-provider strategy requires an ADR and
  differential tests.
- Package vulnerability auditing is part of every dependency change. The
  MSBuild task graph initially resolved a vulnerable transitive
  `System.Security.Cryptography.Xml`; the dependency is explicitly pinned to a
  serviced version and the final graph has no reported vulnerable packages.

The next mechanical layer is restoration of authoritative sibling assembly
partitions, beginning with application services, Web Services/resource
contracts, and data-protection ownership. Removed APIs, design/profile splits,
and behavioral substitutions remain decision-gated.

## 9. Differential compatibility laboratory

Differential testing should be the backbone of the project.

For every fixture:

1. Run the specimen application on .NET Framework 4.8.1.
2. Run the same application source on the modern runtime.
3. Send identical HTTP requests.
4. Capture normalized observable results.
5. Compare results and classify differences.

Captured data should include:

- Status codes and reason behavior
- Header names, order where meaningful, and repeated values
- Cookies and authentication tickets
- Response bytes and declared encoding
- Lifecycle and module event order
- Control tree and generated IDs
- ViewState and event-validation round trips
- Generated client script and resources
- Exception type, message category, and pipeline stage
- Session locking and concurrent-request behavior
- File-system and compilation side effects

Test layers should include:

- API-surface comparison
- Parser and generated-source fixtures
- Unit-level behavioral characterization
- In-process `HttpWorkerRequest` tests
- Real Kestrel integration tests
- Representative specimen applications
- Cross-platform tests
- Security regression tests
- Performance, allocation, and concurrency tests

Snapshots should normalize only values known to vary legitimately, such as generated assembly names or timestamps. Excessive normalization would hide compatibility defects.

## 10. Decision and documentation system

The new repository should contain at least:

```text
docs/
  charter.md
  architecture.md
  compatibility-matrix.md
  deviations.md
  security-model.md
  provenance.md
  adr/
  rfc/
tests/
  fixtures/
  differential/
  hosting/
  security/
```

Each ADR should record:

- Context and compatibility requirement
- Considered options
- Decision
- Consequences
- Security impact
- Platforms affected
- Compatibility profile affected
- Validation and differential tests
- Escape hatch or compatibility switch, if any

Early ADR candidates include:

1. Project compatibility promise and non-goals
2. Assembly names and binary-compatibility policy
3. Reference Source revision and provenance policy
4. `HttpWorkerRequest` as the hosting boundary
5. Runtime compilation and compiler-provider model
6. AppDomain replacement and application isolation
7. `Thread.Abort`, request termination, and timeout semantics
8. Machine-key and data-protection behavior
9. Configuration hierarchy and `web.config` handling
10. File monitoring, restart, and dynamic recompilation
11. Windows-only features and cross-platform profiles
12. Secure defaults versus vulnerable historical behavior

## 11. Security principles

Security is a separate workstream, not a final hardening phase.

High-priority areas include:

- ViewState MAC, encryption, and event validation
- Machine-key generation, persistence, and rotation
- Forms-authentication compatibility
- Legacy cryptographic algorithms
- Request validation
- File upload limits and temporary storage
- Path canonicalization and virtual-to-physical mapping
- Header splitting, duplicate headers, and cookies
- Serialization and `BinaryFormatter` compatibility
- Dynamic compilation and assembly loading
- Configuration secrets
- Authentication, impersonation, and Windows identity
- Denial-of-service behavior under synchronous legacy workloads

Historical behavior should not automatically override secure modern defaults. When exact compatibility would preserve an unsafe behavior, use an explicit ADR, a secure default, an opt-in compatibility switch where defensible, and prominent diagnostics.

## 12. Initial risk register

| Risk | Impact | Initial response |
| --- | --- | --- |
| Strong-named third-party control assemblies | Blocks near-zero migration | Separate source and binary compatibility; build a vendor-control survey |
| AppDomain removal | Affects isolation, reload, shadow copy, static state | Define a process/AssemblyLoadContext model and document limitations |
| Runtime compilation differences | Broad application incompatibility | Preserve parser/codegen; create C# and VB compiler conformance fixtures |
| `Thread.Abort` removal | Lifecycle and timeout differences | Characterize all termination paths before choosing replacement semantics |
| IIS-integrated dependencies | Missing modules and server variables | Define classic-host profile and explicit IIS-only exclusions |
| Configuration hierarchy | Failures during application startup | Build configuration differential tests early |
| Legacy cryptography and serialization | Security and data incompatibility | Byte-level baselines, threat model, compatibility switches |
| Legacy SQL provider types in public APIs | Modern provider types are not interchangeable with compatibility contracts | Preserve legacy API; investigate additive dual-provider strategy through ADR and differential tests |
| Transitive package vulnerabilities | Build/runtime tooling can introduce vulnerable dependencies indirectly | Audit every dependency layer and pin serviced versions explicitly when upstream constraints lag |
| Synchronous pipeline under Kestrel | Thread starvation and poor scalability | Measure first; define bounded scheduling and cancellation model |
| Cross-platform path behavior | Correctness and security defects | Canonical path abstraction plus Windows/Linux differential fixtures |
| Scope explosion | Project never reaches a usable release | Compatibility profiles and vertical milestones |

## 13. Proposed staged roadmap

### Stage 0: Inception and governance

- Approve the charter, terminology, and compatibility dimensions.
- Freeze the POC.
- Select the Reference Source revision.
- Establish licensing and provenance policy.
- Create ADR and RFC templates.
- Select representative applications and third-party controls.

### Stage 1: Baseline and characterization

- Import untouched Reference Source.
- Generate API inventories.
- Produce the compile-error catalog.
- Build the .NET Framework differential runner.
- Characterize the POC's currently working vertical slice.

Repository checkpoint: untouched source import, deterministic original build
inputs, clean diagnostic catalog, POC comparison, and foundational .NET 10
references are complete. API inventory, oracle runner, and vertical-slice
characterization remain.

### Stage 2: Minimal end-to-end runtime

- Build the host-neutral core.
- Implement a tested ASP.NET Core `HttpWorkerRequest` adapter.
- Support one simple ASPX page through Kestrel.
- Add request, response, header, body, path, cancellation, and error tests.

### Stage 3: Core Web Forms compatibility

- Parser and runtime compilation
- Page lifecycle and code-behind
- Standard controls
- ViewState, postback, and event validation
- User controls and master pages
- C# and Visual Basic applications

### Stage 4: Application runtime

- Configuration
- `Global.asax`
- Modules and handlers
- Session, cache, output cache, and application state
- Authentication and authorization
- Resources, themes, routing, and providers

### Stage 5: Ecosystem compatibility

- `System.Web.Extensions` and AJAX behavior
- Optimization and related libraries
- Representative open-source controls
- Commercial-control feasibility and vendor engagement
- Migration analyzer and compatibility report tooling

### Stage 6: Production hardening

- Threat model and penetration testing
- Load, soak, and concurrency testing
- Diagnostics, logging, metrics, and health checks
- Container and supported-OS validation
- Packaging, versioning, upgrade policy, and release documentation

## 14. Initial success criteria

The first meaningful milestone should require all of the following:

- A clean Reference Source baseline with complete provenance.
- A documented API and behavior compatibility scope.
- A specimen Web Forms application whose source is shared between .NET Framework and .NET 10 variants.
- ASPX compilation and execution through real Kestrel integration tests.
- Differential lifecycle, response, ViewState, and postback tests.
- Both C# and Visual Basic runtime compilation.
- No tests operating on repository source directories.
- No silent no-op compatibility shim without an explicit support classification.
- Every semantic deviation linked to an ADR and compatibility-matrix entry.
- A security review of every enabled legacy serialization or cryptographic behavior.

## 15. Immediate next actions

For repository implementation, restore authoritative sibling assembly
partitions next. Do not address the remaining Windows/native, remoting, CAS,
serialization, or hosting errors until their profile/architecture decisions
and validation plans are approved.

1. Validate and reserve the proposed Rehost identity, GitHub organization/repository, NuGet package IDs, and relevant domains; perform trademark review before public release.
2. Freeze and tag the POC at `c9c908f`.
3. Decide whether the product is primarily a long-lived runtime, a migration bridge, or both through separate support profiles.
4. Define the initial application population: framework versions, languages, project types, controls, authentication, and hosting assumptions.
5. Select the pinned Reference Source commit.
6. Create the clean repository with licensing, provenance, charter, ADR, and compatibility templates.
7. Build the Windows .NET Framework 4.8.1 oracle runner before replaying behavioral POC patches.
8. Copy the POC's current simple page and handlers into isolated specimen fixtures.
9. Convert the POC commit history into a candidate-patch ledger.
10. Begin with one fully tested vertical slice rather than broad API compilation alone.

## 16. Open product decisions

- Is the primary purpose indefinite operation of Web Forms on modern .NET, or safe staged migration away from Web Forms?
- What does “minimal changes” allow in project files, configuration, source, and deployment?
- Is Linux support required for the first stable release or a later compatibility profile?
- Must Visual Basic be supported in the first milestone?
- Which third-party control suites determine real-world success?
- Will insecure historical behavior ever be the default?
- Will unsupported APIs fail during build, startup, or first use?
- What public assembly and NuGet package names avoid false binary-compatibility expectations?
- Which ASP.NET-related satellite libraries are in the first stable scope?
- What is the minimum representative legacy application used as a release gate?

## 17. Naming and product-family direction

### 17.1 Selected working name

The selected provisional brand is **Rehost**. The first product is **Rehost WebForms**.

The name was chosen because it is:

- Short, easy to say, and compact in package names.
- Focused on the principal user outcome: rehosting classic ASP.NET applications on modern .NET with minimal changes.
- Broad enough to cover Web Forms, Web API, and supporting classic ASP.NET libraries.
- Distinct from Microsoft product and assembly names, reducing the chance of implying an official Microsoft implementation.
- Compatible with a clear package hierarchy such as `Rehost.WebForms.*` and `Rehost.WebApi.*`.

Proposed identity:

| Item | Working value |
| --- | --- |
| Umbrella brand | Rehost |
| Initial product | Rehost WebForms |
| Repository | `rehost-webforms` |
| Solution | `Rehost.WebForms.slnx` |
| Main meta-package | `Rehost.WebForms` |
| Tagline | Rehost classic ASP.NET applications on modern .NET |

The public documentation should include an independence statement:

> Rehost is an independent open-source project and is not affiliated with, sponsored by, or endorsed by Microsoft.

The exact name remains provisional until GitHub, NuGet, domain, and trademark checks are completed.

### 17.2 Naming alternatives considered

| Candidate | Outcome |
| --- | --- |
| `Rehost` | Selected; concise and directly expresses the migration outcome |
| `Continuum` | Semantically appropriate but considered too long and clunky in package names |
| `ClassicWeb` | Clear and extensible, but emphasizes age rather than the migration outcome |
| `Portico` | Distinctive and product-like, but its purpose requires explanation |
| `Evergreen` | Positive longevity message, but widely used and less specific |
| `Revival` | Memorable, but can sound nostalgic or experimental |
| `OpenWebForms` | Clear for Web Forms, but does not extend elegantly to the broader web stack |
| `System.Web.Core` | Rejected because it sounds official and is easily confused with ASP.NET Core and unrelated “WebForms Core” projects |
| `System.Web.Cross` | Rejected because it sounds like a low-level assembly and describes only portability |
| `WebForms.Next` | Rejected because it implies a successor framework rather than compatibility |

### 17.3 Product-family structure

Web Forms extensions belong under `Rehost.WebForms`. Web API 2 is a sibling product family rather than a Web Forms subsystem. Shared tooling can use `Rehost.WebStack`.

```text
Rehost
├── Rehost.WebForms.*
├── Rehost.WebApi.*
└── Rehost.WebStack.*
```

This prevents misleading names such as `Rehost.WebForms.WebApi.Client`.

### 17.4 Web Forms projects and packages

Project names identify the Rehost implementation, while output assembly names preserve legacy compatibility where practical. NuGet package identity and assembly identity are deliberately separate concerns.

| Project and package | Intended output or responsibility |
| --- | --- |
| `Rehost.WebForms.Runtime` | `System.Web.dll` compatibility runtime |
| `Rehost.WebForms.Extensions` | `System.Web.Extensions.dll` compatibility library |
| `Rehost.WebForms.ApplicationServices` | `System.Web.ApplicationServices.dll` compatibility library |
| `Rehost.WebForms.AspNetCore` | ASP.NET Core and Kestrel hosting adapter |
| `Rehost.WebForms.Compiler` | ASPX, ASCX, and related runtime compilation |
| `Rehost.WebForms.Build` | MSBuild integration, targets, and packaging support |
| `Rehost.WebForms.FriendlyUrls` | `Microsoft.AspNet.FriendlyUrls` compatibility |
| `Rehost.WebForms.Optimization` | `System.Web.Optimization.dll` compatibility |
| `Rehost.WebForms.Optimization.WebForms` | Web Forms integration for optimization |
| `Rehost.WebForms.Infrastructure` | `Microsoft.Web.Infrastructure` compatibility |
| `Rehost.WebForms` | User-facing meta-package |

For example, the core project may use:

```xml
<PropertyGroup>
  <AssemblyName>System.Web</AssemblyName>
  <RootNamespace>System.Web</RootNamespace>
  <PackageId>Rehost.WebForms.Runtime</PackageId>
</PropertyGroup>
```

Producing an assembly named `System.Web` improves source and reflection compatibility but does not reproduce Microsoft's strong-name identity. Binary compatibility remains a separately measured promise.

### 17.5 Shared asset tooling

WebGrease is an asset-processing dependency rather than an intrinsic Web Forms runtime component. Prefer treating it as a private implementation detail of optimization. If it must be published or supported independently, use a shared package such as:

```text
Rehost.WebStack.Assets
```

This permits replacing WebGrease later without unnecessarily making its API part of the primary Web Forms support contract.

### 17.6 Future Web API family

If the project expands to ASP.NET Web API 2, use a sibling family:

| Project and package | Intended output or responsibility |
| --- | --- |
| `Rehost.WebApi.Core` | `System.Web.Http.dll` compatibility |
| `Rehost.WebApi.Formatting` | `System.Net.Http.Formatting.dll` compatibility |
| `Rehost.WebApi.Client` | Web API client support |
| `Rehost.WebApi.WebHost` | Web API hosted inside the compatible `System.Web` pipeline |
| `Rehost.WebApi.AspNetCore` | Potential direct ASP.NET Core hosting adapter |
| `Rehost.WebApi.Owin` | OWIN integration |
| `Rehost.WebApi.Cors` | CORS integration |
| `Rehost.WebApi.Tracing` | Tracing support |
| `Rehost.WebApi` | Web API meta-package |

The first Web API hosting path should favor compatibility:

```text
Kestrel
└── Rehost.WebForms.AspNetCore
    └── System.Web-compatible pipeline
        └── Rehost.WebApi.WebHost
            └── Rehost.WebApi.Core
```

A direct `Rehost.WebApi.AspNetCore` path may later avoid the System.Web pipeline, but it should be treated as a separate architecture and compatibility profile.

### 17.7 Dependency rules

- `Rehost.WebForms.Runtime` must not depend on Friendly URLs, optimization, Web API, or WebGrease.
- Optional Web Forms libraries may depend on the runtime.
- `Rehost.WebApi.Core` must not depend on Web Forms.
- Only `Rehost.WebApi.WebHost` should connect Web API Core to the System.Web-compatible runtime.
- Shared asset and build tooling must not introduce application-runtime dependencies unnecessarily.
- Original assembly names, assembly-qualified configuration values, and strong-name expectations must be catalogued independently from project and package naming.

### 17.8 Solution organization

A monorepo can expose focused solution entry points:

```text
Rehost.WebStack.slnx     # complete integration and release build
Rehost.WebForms.slnx     # Web Forms runtime and extensions
Rehost.WebApi.slnx       # future Web API family
```

Separate repositories can be introduced later if the product families acquire different maintainers or release cadences.

## 18. Final direction

The POC retired the project's primary existential risk: Web Forms' core parsing, compilation, lifecycle, and request pipeline can run on modern .NET behind Kestrel.

The new project's challenge is therefore not to prove possibility again. It is to make compatibility explicit, measurable, secure, and maintainable. The strongest route is to keep Microsoft Reference Source as the semantic implementation baseline, use .NET Framework 4.8.1 as the behavioral oracle, use Mono only as a secondary implementation reference, and replay the POC's discoveries under tests and documented decisions.
