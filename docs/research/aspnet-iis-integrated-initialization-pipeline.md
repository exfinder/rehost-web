# ASP.NET on IIS integrated mode: managed initialization state flow

Background evidence only, not a compatibility contract. Sources target
Microsoft Reference Source revision
`ec9fa9ae770d522a5b5f0607898044b7478574a3`.

The port targets the [classic managed runtime](../classic-managed-runtime-model.md).
Integrated mode remains useful only for identifying shared initialization
postconditions and excluded native dependencies.

## Conclusion

ASP.NET initialization is an ordered protocol:

1. `HttpRuntime.Init` constructs context-free runtime state.
2. `HttpRuntime.HostingInit` establishes hosting, configuration, security, and
   build-system state.
3. `HttpRuntime.FirstRequestInit` creates request-dependent services.

IIS adds native context, callbacks, module inventory, and event subscriptions.
Calling a convenient later method cannot safely replace earlier phases.

## Evidence boundary

Microsoft's [lifecycle overview](https://learn.microsoft.com/en-us/previous-versions/aspnet/bb470252%28v%3Dvs.100%29#life-cycle-stages)
provides the public model. Exact order below comes from pinned Reference Source.
Native `webengine4.dll` internals are excluded unless a managed interface,
callback, or source comment proves the edge.

## State flow

| Stage | Required order and postconditions | Evidence |
| --- | --- | --- |
| 1. Process host | IIS creates one default-domain `ProcessHost` and `ApplicationManager`, publishes support functions, and has not created an application AppDomain. | [factory](../../src/System.Web.ReferenceSource/Hosting/ProcessHostFactoryHelper.cs#L22-L58), [construction](../../src/System.Web.ReferenceSource/Hosting/ProcessHost.cs#L288-L350), [support functions](../../src/System.Web.ReferenceSource/Hosting/IProcessHostSupportFunctions.cs#L19-L66) |
| 2. Application start | `ProcessHost.StartApplication` serializes by application ID and asks `ApplicationManager` for `PipelineRuntime`; obtaining the returned handle first completes application environment creation. | [start](../../src/System.Web.ReferenceSource/Hosting/ProcessHost.cs#L680-L752), [creation](../../src/System.Web.ReferenceSource/Hosting/ApplicationManager.cs#L275-L294), [runtime start](../../src/System.Web.ReferenceSource/Hosting/IPipelineRuntime.cs#L235-L255) |
| 3. AppDomain binding | Under an application lock, `ApplicationManager` resolves paths/configuration, creates the AppDomain, publishes five data slots, initializes `HostingEnvironment`, then creates `PipelineRuntime`. The partially published environment is hidden by this sequence. | [AppDomain setup](../../src/System.Web.ReferenceSource/Hosting/ApplicationManager.cs#L880-L958), [activation](../../src/System.Web.ReferenceSource/Hosting/ApplicationManager.cs#L1180-L1321), [bindings](../../src/System.Web.ReferenceSource/Hosting/ApplicationManager.cs#L1667-L1688) |
| 4. `HttpRuntime.Init` | First runtime access loads native capability state, constructs `HttpRuntime`, reads application slots, and creates callbacks, timeout management, counters, and `FileChangesMonitor`. Errors are cached rather than immediately rethrown. | [static init](../../src/System.Web.ReferenceSource/HttpRuntime.cs#L98-L176), [`Init`](../../src/System.Web.ReferenceSource/HttpRuntime.cs#L286-L348), [hosting entry](../../src/System.Web.ReferenceSource/Hosting/HostingEnvironment.cs#L279-L343) |
| 5. Configuration install | `HostingEnvironment` installs the global configuration system incomplete; `HostingInit` completes it after trust setup. Querying `UseHttpConfigurationSystem` first irreversibly selects the client path. | [hosting sequence](../../src/System.Web.ReferenceSource/Hosting/HostingEnvironment.cs#L313-L365), [install/complete](../../src/System.Web.ReferenceSource/Configuration/HttpConfigurationSystem.cs#L64-L134), [selection](../../src/System.Web.ReferenceSource/Configuration/HttpConfigurationSystem.cs#L136-L166) |
| 6. `HostingInit` | In Framework order: data/access, monitoring, cache host, minimal configuration, codegen, trust, Fusion/URL state, configuration completion, process policy, keys, then `BuildManager`. Failure is cached as hosting initialization failure. | [`HostingInit`](../../src/System.Web.ReferenceSource/HttpRuntime.cs#L389-L578) |
| 7. Pre-application hooks | After `HostingInit`, `ExecutePreAppStart` and `App_Code.AppInitialize` run before `PipelineRuntime` is returned. | [hook order](../../src/System.Web.ReferenceSource/Hosting/HostingEnvironment.cs#L365-L407), [pre-start](../../src/System.Web.ReferenceSource/Compilation/BuildManager.cs#L752-L816), [dispatch](../../src/System.Web.ReferenceSource/Compilation/BuildManager.cs#L1144-L1162), [invocation](../../src/System.Web.ReferenceSource/Compilation/BuildResult.cs#L680-L735) |
| 8. Integrated application init | IIS supplies native application context. ASP.NET reflects `Global.asax`, creates a special application, obtains native modules, registers events, calls `Application_Start`, then initializes derived application/modules. Failure is cached for a later managed error response. | [entry](../../src/System.Web.ReferenceSource/Hosting/IPipelineRuntime.cs#L302-L377), [application creation](../../src/System.Web.ReferenceSource/HttpApplicationFactory.cs#L388-L430), [exact order](../../src/System.Web.ReferenceSource/HttpApplication.cs#L2396-L2490) |
| 9. First request | Native notification creates worker/context state, then `FirstRequestInit` establishes enabled/offline checks, codegen access, health, queue, tracing, bin preload, encoder, and validator before normal `HttpApplication` allocation. | [bridge](../../src/System.Web.ReferenceSource/Hosting/IPipelineRuntime.cs#L477-L590), [request entry](../../src/System.Web.ReferenceSource/HttpRuntime.cs#L1434-L1510), [first-request init](../../src/System.Web.ReferenceSource/HttpRuntime.cs#L620-L727), [normal application](../../src/System.Web.ReferenceSource/HttpApplicationFactory.cs#L344-L388) |

## Decisions derived from this evidence

- [Runtime compatibility model](../adr/0001-runtime-compatibility-model.md).
- [Application lifecycle](../adr/0002-application-lifecycle.md).
- [Configuration and compilation](../adr/0004-configuration-and-compilation.md).
- [Host boundary](../adr/0003-host-boundary.md).
