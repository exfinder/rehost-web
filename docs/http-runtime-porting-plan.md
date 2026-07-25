# HttpRuntime portability implementation plan

Status: proposed. Owner:
[request-startup portability](follow-ups/request-startup-portability.md).

Evidence:
[IIS integrated initialization research](research/aspnet-iis-integrated-initialization-pipeline.md).

## Approach

Keep application bootstrap complete. Port startup phase-by-phase using .NET
Framework postconditions as the compatibility oracle. Do not flatten
`HttpRuntime.Init`, `HostingInit`, pipeline initialization, and
`FirstRequestInit`.

Keep platform conditionals at leaf operations rather than bypassing lifecycle
callers. Every native operation must have a portable semantic replacement, an
initialized no-op with an approved contract, or an explicit unsupported
failure.

## Immediate corrections

1. Correct compatibility documentation: integrated `Application_Start` is
   pipeline-initialization behavior, not ordinary first-request behavior.
2. Prove bootstrap mapped preflight does not install
   `HttpConfigurationSystem` or initialize `HttpRuntime`.

## Phase 1: runtime construction

Port `HttpRuntime.StaticInit` and `Init`.

Required postconditions:

- identity and path fields populated;
- timeout manager constructed;
- completion callbacks assigned;
- profiler policy explicit;
- file-monitor object valid even when watching is disabled;
- no cached initialization error.

## Phase 2: hosting initialization

Preserve `HostingEnvironment.Initialize` ordering:

1. install live global mapped configuration;
2. initialize cache;
3. establish codegen and assembly-loading paths;
4. establish explicit full trust;
5. complete configuration;
6. initialize generated keys and `BuildManager`.

This is where bootstrap configuration becomes live runtime configuration.

## Phase 3: pre-application hooks

After successful `HostingInit`:

1. execute `PreApplicationStartMethod`;
2. compile top-level and `App_Code` files;
3. invoke App_Code `AppInitialize`;
4. prevent pipeline initialization after failure.

## Phase 4: pipeline initialization

Define a host-neutral replacement for IIS
`PipelineRuntime.InitializeApplication`.

Recommended compatibility profile: IIS integrated ordering, the normal modern
.NET Framework production behavior.

Required postconditions:

- `Global.asax` compiled;
- module inventory constructed;
- `Application_Start` called exactly once with an initialization context;
- application and module initialization completed;
- host-neutral event subscriptions published.

## Phase 5: first-request initialization

Port request-dependent work after pipeline readiness:

- application enabled/offline checks;
- compilation-directory access;
- request queue policy;
- encoder and validator;
- health and tracing policy;
- normal `HttpApplication` allocation.

## Partial-state controls

For every phase:

- define explicit postconditions;
- validate required private collaborators before marking the phase complete;
- require the previous phase at every later entry;
- use initialized no-op implementations only for approved absent semantics;
- throw for unsupported semantics;
- never skip an assignment only because its original implementation is native;
- make failure terminal and prevent every later phase/request.

## Verification

Each phase receives:

- successful postcondition tests;
- ordering tests;
- injected failure tests;
- proof that failure prevents later phases;
- tests for every portable replacement or initialized no-op.

Avoid one large `HttpRuntime` patch. Deliver runtime construction, hosting
initialization, application hooks, pipeline initialization, and first-request
services as independently reviewable units.
