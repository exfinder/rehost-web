# ASP.NET on IIS integrated mode: managed initialization state flow

Status: research note. Target: pinned .NET Framework Reference Source revision
`ec9fa9ae770d522a5b5f0607898044b7478574a3`.

## Conclusion

ASP.NET initialization is not one method. It is an ordered, three-phase protocol
whose participants publish interdependent static state:

1. `HttpRuntime.Init`: AppDomain-local, context-free runtime construction.
2. `HttpRuntime.HostingInit`: hosting/configuration/security/build-system setup.
3. `HttpRuntime.FirstRequestInit`: request-context-dependent services.

Around these phases IIS establishes a fourth kind of state: integrated-pipeline
native context, callbacks, module inventory, and event subscriptions.

Porting by setting only application paths and calling a convenient later method
is unsafe. Many private fields and global collaborators would remain unset.
The safe approach is to retain the phase ordering and replace each Windows/IIS
operation behind a host-neutral equivalent or an explicit unsupported failure.

## Evidence boundary

Microsoft's lifecycle documentation gives the public model: first request causes
application-domain/hosting-environment creation, top-level compilation, then
request processing through the unified IIS pipeline. It is intentionally
high-level and compresses several managed phases
([Microsoft lifecycle overview](https://learn.microsoft.com/en-us/previous-versions/aspnet/bb470252%28v%3Dvs.100%29#life-cycle-stages)).

The exact managed order below comes from Microsoft's pinned Reference Source.
The native implementation of `webengine4.dll` is not published there. Native
steps are stated only where managed COM interfaces, callbacks, or source
comments prove the edge; details inside native IIS are not inferred.

## Exact state flow

### 1. IIS obtains the process host

Native hosting calls `IProcessHostFactoryHelper.GetProcessHost`, which delegates
to the process-wide `ProcessHost` singleton. Construction stores
`IProcessHostSupportFunctions`, publishes them to the default-domain
`HostingEnvironment`, and obtains the default-domain `ApplicationManager`.
Those support functions are the IIS-owned source for application properties,
mapping, configuration tokens, and native configuration
([ProcessHostFactoryHelper](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/ProcessHostFactoryHelper.cs#L22-L58),
[ProcessHost construction](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/ProcessHost.cs#L288-L350),
[support-functions contract](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/IProcessHostSupportFunctions.cs#L19-L66)).

Postconditions:

- one `ProcessHost` in the default AppDomain;
- one default-domain `ApplicationManager`;
- IIS support functions available to managed hosting;
- no application AppDomain yet.

### 2. IIS starts one application

For integrated mode native hosting calls `IProcessHost.StartApplication(appId,
appPath, out runtimeInterface)`. `ProcessHost` normalizes the physical path,
creates an `ISAPIApplicationHost`, serializes work by application ID, optionally
preloads, then asks `ApplicationManager` to create a `PipelineRuntime`.
`PipelineRuntime.StartProcessing` installs the stop-listening handler; the
returned remoting handle is the native runtime interface
([StartApplication](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/ProcessHost.cs#L680-L752),
[CreateObjectInternal](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/ApplicationManager.cs#L275-L294),
[PipelineRuntime.StartProcessing](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/IPipelineRuntime.cs#L235-L255)).

Postcondition: IIS has not merely received a callback object. Creating that
object first forces complete application-AppDomain and `HostingEnvironment`
creation described below.

### 3. `ApplicationManager` creates and binds the application AppDomain

Under a per-application lock, `ApplicationManager`:

1. obtains physical/virtual paths;
2. opens application configuration in the default AppDomain;
3. builds `AppDomainSetup`, trust/security inputs, and compatibility switches;
4. creates the child AppDomain;
5. writes `.appDomain`, `.appId`, `.appPath`, `.appVPath`, and `.domainId`;
6. activates `HostingEnvironment` in it;
7. calls `HostingEnvironment.Initialize`;
8. only then creates the requested well-known `PipelineRuntime`.

The setup also establishes application base/name, private probing, shadow copy,
and `web.config` as the AppDomain configuration filename
([AppDomain creation](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/ApplicationManager.cs#L880-L958),
[activation and Initialize](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/ApplicationManager.cs#L1180-L1321),
[domain bindings](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/ApplicationManager.cs#L1667-L1688)).

Postconditions before `HostingEnvironment.Initialize`:

- AppDomain loader/security policy fixed;
- all five application data slots published;
- `HostingEnvironment` constructor has already published its singleton and
  installed unload/unhandled-exception handlers;
- most `HostingEnvironment` instance fields remain unset.

This temporarily partial singleton is safe only because `ApplicationManager`
controls the sequence. It is not a state that a new host should expose.

### 4. `HostingEnvironment.Initialize` triggers `HttpRuntime` phase 1

`HostingEnvironment.Initialize` first stores hosting parameters and manager
references. It then reads application identity/path properties through
`HttpRuntime`. That first static access runs the `HttpRuntime` type initializer:

1. `StaticInit` locates/loads `webengine`, initializes native library/performance
   support, and reads IIS version/integrated-mode information;
2. constructs the singleton `HttpRuntime`;
3. calls `HttpRuntime.Init`.

`Init` constructs profiler, timeout manager, callbacks, reads the five AppDomain
slots into runtime fields, determines UNC status, opens per-application
performance counters, and constructs `FileChangesMonitor`. Exceptions are
cached in `HttpRuntime.InitializationException`, not immediately rethrown
([HttpRuntime cctor and StaticInit](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/HttpRuntime.cs#L98-L176),
[HttpRuntime.Init](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/HttpRuntime.cs#L286-L348),
[HostingEnvironment initialization entry](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/HostingEnvironment.cs#L279-L343)).

Phase-1 postconditions:

- runtime identity/path fields populated;
- timeout manager and request/native completion callbacks allocated;
- file-change monitor exists and already depends on application path and FCN
  hosting parameters;
- IIS mode/version may be populated;
- initialization error may be cached while construction continues.

### 5. Web configuration system is installed, then completed later

Back in `HostingEnvironment.Initialize`, ASP.NET creates `IConfigMapPath`, calls
`HttpConfigurationSystem.EnsureInit`, caches `IConfigMapPath2`, notifies the
manager, stores the application host/config token, and installs the initial
map-path virtual-path provider.

`EnsureInit` constructs `WebConfigurationHost`, config root and internal
`ConfigSystem`, optionally attaches change events, and replaces the process
`ConfigurationManager` configuration system. It deliberately marks this system
incomplete. `CompleteInit` occurs only after trust is established in
`HttpRuntime.HostingInit`
([HostingEnvironment configuration sequence](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/HostingEnvironment.cs#L313-L365),
[HttpConfigurationSystem.EnsureInit/CompleteInit](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Configuration/HttpConfigurationSystem.cs#L64-L134)).

Important circularity: querying `UseHttpConfigurationSystem` before
`EnsureInit` irrevocably marks initialization attempted and selects the client
configuration path. Configuration access order is therefore semantic, not
cosmetic
([UseHttpConfigurationSystem](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Configuration/HttpConfigurationSystem.cs#L136-L166)).

### 6. `HttpRuntime.HostingInit` establishes phase 2

`HostingEnvironment` now calls `HttpRuntime.InitializeHostingFeatures`, which
runs `HostingInit` under application impersonation. In order, it:

1. sets first-start time and `DataDirectory`;
2. verifies application-directory access;
3. starts critical directory/bin monitoring;
4. installs `ObjectCacheHost`;
5. reads minimal cache/trust/security/compilation/hosting configuration;
6. creates and verifies the code-generation directory;
7. establishes trust/CAS state;
8. initializes Fusion probing and URL metadata caching;
9. completes `HttpConfigurationSystem`;
10. applies thread-pool limits and auto-generated keys;
11. initializes `BuildManager`;
12. initializes compilation profiling, apartment threading, debugging,
    request-trust flags and AppDomain resource counters.

Any exception sets both `_hostingInitFailed` and the shared initialization
exception; `ThrowHostingInitErrors` controls whether it also escapes
immediately
([HostingInit](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/HttpRuntime.cs#L389-L578)).

Phase-2 postconditions:

- configuration system complete and globally installed;
- trust policy finalized before arbitrary section handlers run;
- codegen/Fusion/BuildManager initialized coherently;
- cache, file monitoring, generated keys and runtime callbacks available;
- failure represented as terminal AppDomain initialization state, not rollback.

### 7. Pre-application hooks run before integrated pipeline registration

After `HostingInit`, `HostingEnvironment.Initialize` establishes idle
monitoring, application identity and application monitors. If hosting init
succeeded it calls:

1. `BuildManager.ExecutePreAppStart`;
2. `BuildManager.CallAppInitializeMethod`, unless explicitly disabled.

Pre-application methods are found on referenced assemblies and invoked as
public static parameterless methods. In the pinned implementation they are
sorted by declaring assembly-qualified type and method name. Only after that
stage may top-level compilation proceed. `App_Code` `AppInitialize` forces
top-level compilation and invokes the single discovered public static
parameterless method
([HostingEnvironment hook order](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/HostingEnvironment.cs#L365-L407),
[pre-start execution](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Compilation/BuildManager.cs#L752-L816),
[AppInitialize dispatch](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Compilation/BuildManager.cs#L1144-L1162),
[method discovery/invocation](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Compilation/BuildResult.cs#L680-L735)).

Postcondition: package pre-start code and `App_Code` initialization have already
run before the `PipelineRuntime` object is returned to IIS.

### 8. IIS initializes the integrated application; `Application_Start` runs here

After `StartApplication` returns the runtime interface, native IIS calls
`IPipelineRuntime.InitializeApplication(appContext)`. The method:

1. stores native application context;
2. refreshes IIS version/integrated-mode state (needed because preload can run
   before IIS module registration);
3. creates a dummy `SimpleWorkerRequest` and `HttpContext`;
4. compiles/reflects `Global.asax`;
5. creates a special `HttpApplication`;
6. obtains the native integrated module list;
7. registers managed event subscriptions with IIS;
8. invokes `Application_Start` once;
9. calls derived `HttpApplication.Init`;
10. calls each configured module's `Init` and registers its subscriptions;
11. marks integrated initialization completed.

`Application_Start` is therefore not left to normal first-request dispatch in
integrated mode. It runs during native application initialization, with a dummy
context whose request/response are hidden. Source places it before derived
`HttpApplication.Init` and before the loop invoking each `IHttpModule.Init`
([PipelineRuntime.InitializeApplication](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/IPipelineRuntime.cs#L302-L377),
[pipeline application creation](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/HttpApplicationFactory.cs#L388-L430),
[integrated registration and exact order](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/HttpApplication.cs#L2396-L2490)).

If this phase fails, ASP.NET caches the exception and registers special native
subscriptions so a later request reaches managed code and receives the
formatted initialization error. It does not pretend initialization succeeded.

Integrated-pipeline postconditions:

- native `appContext` stored;
- integrated-mode/version state refreshed;
- `Global.asax` compiled and reflected;
- native module inventory cached;
- managed/native event subscriptions registered;
- `Application_Start` attempted exactly once;
- initialization completion/error state published.

### 9. First request notification triggers phase 3

The first native notification enters `PipelineRuntime`, which creates
`IIS7WorkerRequest`, `HttpContext`, and rooted native/managed request state, then
calls `HttpRuntime.ProcessRequestNotification`.

Under a once-only lock, `FirstRequestInit`:

- ensures configuration;
- checks application enabled/offline state;
- checks codegen-directory access under hosting identity;
- initializes health monitoring, request queue, tracing/profiling heartbeat;
- restricts IIS folders;
- optionally preloads bin assemblies;
- initializes header checking, encoder, and request validator.

Only after this phase does ASP.NET obtain a normal pooled `HttpApplication`,
initialize its per-instance modules and steps, associate it with the request,
and run the request notification
([native notification bridge](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Hosting/IPipelineRuntime.cs#L477-L590),
[integrated request entry](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/HttpRuntime.cs#L1434-L1510),
[FirstRequestInit](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/HttpRuntime.cs#L620-L727),
[normal application instance](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/HttpApplicationFactory.cs#L344-L388)).

## Semantic hazards for Rehost

1. **Do not flatten the three `HttpRuntime` phases.** Later fields assume earlier
   postconditions: file monitoring assumes app path; BuildManager assumes trust,
   Fusion and codegen; request execution assumes callbacks, queue, encoder and
   validator.

2. **Do not install the global configuration system during preflight.** The real
   installation is two-phase and order-sensitive. An isolated mapped
   `Configuration` object is safe to discard; `EnsureInit` is global and not.

3. **Do not expose a partially initialized `HostingEnvironment`.** Framework
   publishes its singleton in the constructor, then relies on controlled
   AppDomain activation to finish it. Same-AppDomain hosting needs a stronger
   external gate.

4. **Do not treat cached initialization exceptions as recoverable.** Framework
   recovers by unloading/replacing the application AppDomain. With one
   application in the current AppDomain, process failure/replacement is the
   semantic equivalent.

5. **Port integrated-pipeline state explicitly.** Leaving
   `UseIntegratedPipeline=false`, native context zero, module inventory empty,
   or notification registration absent changes `HttpApplication` construction
   and event behavior. A Kestrel adapter needs host-neutral equivalents, not
   default-valued fields.

6. **Preserve startup-hook placement.** `PreApplicationStartMethod` and
   `App_Code.AppInitialize` belong after HostingInit/BuildManager setup and
   before pipeline application initialization. Integrated `Application_Start`
   belongs in pipeline initialization, before the first real request.

7. **Separate native operation from semantic postcondition.** Performance
   counters, IIS health monitor, registry policy, impersonation, FCN, native
   module enumeration, and event registration are platform operations. Each
   must be replaced, deliberately declared unnecessary, or rejected. Merely
   skipping the call risks a later null/default-state failure.

## Recommended port verification

Implement phase-level assertions/tests, not only first-page success:

- after runtime construction: identity/path, callbacks and platform capability
  state complete;
- after hosting init: configuration complete, full-trust state explicit,
  codegen/Fusion/BuildManager ready;
- after pre-app hooks: each hook category called once in Framework order;
- after pipeline init: `Application_Start` once, module inventory and event
  subscriptions complete;
- after first request init: request services complete before normal
  `HttpApplication` allocation;
- inject failure in every phase and prove no later phase or request dispatch
  runs.
