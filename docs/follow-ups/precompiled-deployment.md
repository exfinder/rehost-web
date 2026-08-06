# Precompiled deployment

Status: not started (2026-08-06). PROJECT.md already scopes this:
"Precompilation should be a separate build-time or process-level facility."
This records what exists, the shape of the work, and the decisions it needs.
Nothing here is committed to.

## Motivation

With Roslyn prejitted and a `PublishReadyToRun` publish, the sample
application's first request measures ~600 ms against an empty codegen root and
~150 ms against a warm one ([roslyn-compile-cold-start](roslyn-compile-cold-start.md)).
The remaining ~450 ms is real page compilation, paid on every process start —
and process replacement is the restart model. Framework's answer was
`aspnet_compiler`: compile at deployment time, serve from precompiled
assemblies, never invoke the compiler in production. A fully precompiled
non-updatable site also needs no page sources deployed and, in principle, no
Roslyn in the publish at all (~30 MB of R2R images plus the compile
infrastructure).

## What the imported source already has

Both halves of Framework's mechanism are in the imported tree:

- **Consumer (serving a precompiled app)** — `BuildManager` detects the
  `PrecompiledApp.config` marker (`IsPrecompiledApp`,
  `IsNonUpdatablePrecompiledApp`), loads results from `.compiled`
  preservation files and `App_Web_*` assemblies in `bin`, and distinguishes
  updatable from non-updatable layouts. This path is compiled today but has
  never been exercised in the port.
- **Producer (creating one)** — `BuildManager` precompilation runs when
  `HostingEnvironmentParameters.PrecompilationTargetPhysicalDirectory` is set,
  driven by `PrecompilationFlags` through `ClientBuildManagerParameter`.

The producer's *driver*, however, is `ClientBuildManager`: a
`MarshalByRefObject` that hosts a second AppDomain — exactly the machinery the
architecture replaces with process-per-application. A port-native producer
would keep the `BuildManager` precompilation entry points and replace the
driver with a separate process (a dotnet tool or MSBuild task hosting the
runtime against the app root, writing the target layout), consistent with how
process replacement already substitutes for AppDomain cycling.

## Rejected shortcut: shipping a warm codegen root

Copying a built `codegen/` directory into the publish breaks by construction:
`CodegenDirectory.GenerationSegment` keys on a SHA-256 of the application's
physical path, so any deployment path other than the build machine's misses,
and preservation-file timestamp checks make copied sources hazardous. At most
it is a container-image trick (path baked and immutable) — not a mechanism.
The precompiled layout is path-independent; that is why Framework shaped it
that way.

## Contract questions to settle before building

1. Producer packaging: dotnet tool, MSBuild target in the consumer's publish,
   or both. Deployment is part of architecture; this needs the same
   determinism treatment as the R2R targets.
2. Updatable precompilation (`-u`): Framework allowed aspx markup edits on a
   precompiled site. Supporting both layouts doubles the test surface; the
   non-updatable one carries most of the value.
3. Whether a fully precompiled publish may omit Roslyn, and how the runtime
   reports an attempt to compile on such a deployment (fail fast with an
   actionable error, per the portability contract — not a silent fallback).
4. What `IsPrecompiledApp` behavior is *supported*: the consumer path has
   never run cross-platform; case sensitivity, path mapping, and assembly
   loading through `.compiled` files all need the standard two-platform
   evidence before any compatibility claim.
5. Trade to document for consumers: precompiled deployment gives up
   edit-aspx-and-restart. It is a deployment mode beside, not instead of,
   runtime compilation.

A sensible first step is evidence, not code: hand-build a minimal precompiled
layout on Framework (`aspnet_compiler` on the oracle), point the port's
consumer path at it, and see how far `IsPrecompiledApp` serving gets. That
sizes the consumer gap before any producer design is argued about.

Related: [roslyn-compile-cold-start](roslyn-compile-cold-start.md),
[runtime-codegen-and-loading](runtime-codegen-and-loading.md),
[web-site-vs-wap-project-models](web-site-vs-wap-project-models.md).
