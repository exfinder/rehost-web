# Friendly URLs dependency inventory

## Result

`Microsoft.AspNet.FriendlyUrls.Core` 1.0.2 had no missing `System.Web` API type
in the runtime, but its strong-named Framework binary was unusable. The resulting
source-compatible `Rehost.AspNet.FriendlyUrls` package now preserves the
public API without imitating Microsoft identity. The frozen application consumes
that package and builds through Friendly URLs to the next missing dependency.

The restored official package was inspected without decompiling method bodies:
NuSpec, XML documentation, PE assembly/type/member references, manifest
resources, and literal metadata. Package SHA-256:
`46b2a581390346060e23cab873cd87fca042ef856576c6c16c5a94ffcd42434e`;
net45 DLL SHA-256:
`7f7411fc40c272d0357717f188f553ff56fd5b3c08bf7ea3dd189713d6f0e977`.
The package targets only .NET Framework; its net45 group declares no NuGet
dependency. The net40 group alone depends on `Microsoft.Web.Infrastructure`
([official package](https://www.nuget.org/packages/Microsoft.AspNet.FriendlyUrls.Core/1.0.2)).

## Application-reached surface

The frozen application directly reaches only:

- `FriendlyUrlSettings()`, `AutoRedirectMode`, `RedirectMode.Permanent`, and
  `EnableFriendlyUrls(RouteCollection, FriendlyUrlSettings)` during
  `Application_Start` ([registration](../../../apps/WebFormsApplication/WebFormsApplication/App_Start/RouteConfig.cs#L11-L15)).
- `WebFormsFriendlyUrlResolver.IsMobileView(HttpContextBase)`, the named switch
  route, and outbound route generation when the mobile master renders
  ([switcher](../../../apps/WebFormsApplication/WebFormsApplication/ViewSwitcher.ascx.cs#L20-L40),
  [mobile master](../../../apps/WebFormsApplication/WebFormsApplication/Site.Mobile.Master#L1-L20)).
- Extensionless `/About` and `/Contact` requests from literal navigation links
  ([desktop master](../../../apps/WebFormsApplication/WebFormsApplication/Site.Master#L40-L52)).

Official documentation confirms that `EnableFriendlyUrls` adds Web Forms
friendly routing, the resolver tries ordered physical extensions, and mobile
selection considers browser detection or an override
([EnableFriendlyUrls](https://learn.microsoft.com/en-us/previous-versions/aspnet/jj879418%28v%3Dvs.111%29),
[resolver contract](https://learn.microsoft.com/en-us/previous-versions/aspnet/jj879390%28v%3Dvs.100%29),
[mobile decision](https://learn.microsoft.com/en-us/previous-versions/aspnet/jj879414%28v%3Dvs.111%29)).
Defaults reached implicitly are static resolver caching, switch route name
`AspNet.FriendlyUrls.SwitchView`, and URL
`__FriendlyUrls_SwitchView/{view}`
([settings](https://learn.microsoft.com/en-us/previous-versions/aspnet/jj879437%28v%3Dvs.100%29)).

`FriendlyUrl.Href`, `FriendlyUrl.Resolve`, `FriendlyUrl.Segments`, generic
handler resolution, custom resolvers, model binding, and dynamic/disabled cache
modes are not reached by this application.

## Pre-implementation dependency classification

`Available` below records whether referenced type and member names existed in
the compiled runtime before implementation; it is not a current behavior claim.
Current support lives in the
[compatibility map](../compatibility.md#requests-pages-and-responses).

| Phase | Implicit dependency | State | Minimum implication |
| --- | --- | --- | --- |
| Compile | `System.Web` types referenced by the DLL | **Available after recompile** | Metadata found every referenced type/member name in `Rehost.Web`; binary assembly identity remains incompatible. |
| Startup | `PreApplicationStartMethod`, `HttpApplication.RegisterModule`, post-map event | **Available, uncertain integration** | Core metadata registers a pre-start hook and contains a redirect module. Pre-start execution is supported, but package discovery/copy in the WAP host needs an integration gate. |
| Startup | `RouteTable`, `RouteCollection`, `RouteBase`, `Route`, route names | **Available** | Add friendly inbound route and switch-view route in Framework order; preserve settings defaults. |
| Request routing | `UrlRoutingModule` | **Missing registration** | The class exists, but the shipped root configuration omits all default modules. Restore it in the runtime root configuration at its canonical Framework position; Friendly URLs owns routes, not module activation ([module boundary](../follow-ups/shipped-http-modules.md#what-registration-does-not-settle), [module contract](https://learn.microsoft.com/en-us/dotnet/api/system.web.routing/urlroutingmodule?view=netframework-4.8.1)). |
| Request routing | `RouteData`, `RequestContext`, `RouteValueDictionary`, `IRouteHandler`, `IHttpHandler` | **Available, unassessed** | Required for inbound match, segment data, handler creation, and switch-route URL generation. Outbound escaping already has an unresolved parity item ([escaping](../follow-ups/route-url-escaping.md)). |
| Handler creation | `BuildManager.GetObjectFactory`, `IWebObjectFactory`, mapped virtual paths | **Available, partly proven** | Runtime page compilation is supported; Friendly URL creation of an `.aspx` handler through this exact path is not tested. |
| Files/cache | `HostingEnvironment.VirtualPathProvider`, virtual directory enumeration, `HttpRuntime.Cache`, `CacheDependency`, `IRegisteredObject` | **Available, unassessed** | Default static mode enumerates the application and participates in host lifetime. Implement all three public cache modes; focused tests must distinguish static, dynamic, and disabled behavior. |
| Configuration | `WebConfigurationManager`, `CompilationSection.Debug`, virtual-path utility | **Available, partly proven** | Mapped application configuration and path utilities exist; Friendly URL-specific use needs focused tests. |
| Security | `UrlAuthorizationModule.CheckUrlAccessForPrincipal`, `User`, `SkipAuthorization`, authorization-failure handler | **Available, unassessed** | Do not treat “no rules in stock app” as proof. At least allow and deny cases must precede a support claim; URL authorization is currently unassessed. |
| Redirect | `HttpResponse.Redirect`, `RedirectPermanent`, query/path encoding | **Available, proven** | Stock settings reach permanent redirect from `.aspx`; observable 301 and `Location` are pinned by scenario tests. Corrected by the 1.0.2 binary reading: the module's rewritten-request guard is a `RawUrl`-contains-`CurrentExecutionFilePath` comparison — redirect only when the client literally typed the extension URL; a rewritten request (IIS default document, URL rewriting) is instead served through the friendly route with its route data stashed in `Items`. The IIS server variables (`IIS_UrlRewriteModule`/`IIS_WasUrlRewritten`) live only in upstream's `UrlRewriterHelper` (AND semantics) for client-URL generation, and IIS does not set `IIS_WasUrlRewritten` for default-document rewrites (reading D16), so the host returning `null` for both stays correct. |
| Mobile | `HttpBrowserCapabilitiesBase.IsMobileDevice`, cookies, `Page.PreInit`, `Page.MasterPageFile`, virtual-file checks | **Available, partial/uncertain** | Cookies and ordinary masters are proven. Browser classification and runtime mobile-master selection are unassessed. The resolver's contract explicitly includes mobile-master substitution ([mobile master API](https://learn.microsoft.com/en-us/previous-versions/aspnet/jj879421%28v%3Dvs.111%29)). |
| Packaging | FriendlyUrls core DLL plus meta-package content | **Core replacement required; content already present** | The meta-package is WAP content, not a runtime assembly; the frozen app already contains `Site.Mobile.Master` and `ViewSwitcher` ([official meta-package](https://www.nuget.org/packages/Microsoft.AspNet.FriendlyUrls/1.0.2)). Do not install or regenerate that content. |

`Microsoft.Web.Infrastructure` is therefore not a Friendly URLs dependency for
the app's selected net45 asset. No other external assembly dependency was found:
the core DLL references only `mscorlib`, `System`, `System.Core`, and
`System.Web`.

## Implementation scope

1. Package and assembly `Rehost.AspNet.FriendlyUrls`, preserving the complete
   original public API under `Microsoft.AspNet.FriendlyUrls`; depend only on
   `Rehost.Web`. Do not imitate Microsoft's binary identity.
2. Implement settings/default validation, route registration, `.aspx`
   extensionless resolution, segment capture, handler creation/preprocessing,
   permanent physical-URL redirect, and the named view-switch route.
3. Restore `UrlRoutingModule` in the runtime root configuration, ordered as in
   Framework's authoritative root configuration. Merely adding routes is
   insufficient.
4. Implement browser/cookie override, `.Mobile.aspx` preference, mobile-master
   selection at `Page.PreInit`, safe local return redirect, and switch cookie.
5. Implement generic handlers, public helper/model-binding APIs, custom
   resolvers, and static, dynamic, and disabled caching. Preserve original
   semantics as far as evidence permits; test by behavior family rather than
   exhaustively.
6. Preserve the original IIS rewrite-variable checks without adding an IIS or
   Windows dependency. Do not expand the host adapter in this story.

## Candidate Framework probes

Use these only where public documentation, metadata, and portable integration
tests leave observable behavior ambiguous. Capture small result tables, not a
reusable oracle or decompiled implementation:

1. Fresh `RouteCollection` before/after registration: count, order, names,
   patterns, defaults, route types; default settings; duplicate/null/error cases
   only where implementation needs them.
2. Requests `/About`, `/About/one/two`, `/Missing`, `/About.aspx?x=1`, and
   `/Default.aspx`: status, handler/page, route data, segments, redirect location,
   and query preservation.
3. Allow and `<location>` deny for the same friendly page.
4. Desktop/mobile user agents with no override, then Desktop/Mobile switch URL:
   route generation, cookie name/value/flags, local return redirect, chosen
   `.aspx` file, and chosen master.
5. Cold concurrent first requests under default static caching. Probe file
   mutation under each mode only if portable tests cannot establish its intended
   semantics.
6. URL/path cases needed by the existing escaping follow-up: spaces, Unicode,
   slash/segment values, existing escapes, query/fragment, and malicious
   `ReturnUrl` values.

## Accepted implementation boundaries

- Evidence: public documentation, PE/API metadata, XML documentation, and
  black-box Framework readings. Do not inspect ILSpy method bodies; reconsider
  narrowly only if indispensable interoperability behavior cannot otherwise be
  discovered.
- API: implement the complete original public API and behavior. Testing is
  pragmatic: stock-app integration plus focused coverage by behavior family.
- Routing: the runtime root configuration owns `UrlRoutingModule` registration
  and preserves Framework module ordering. Friendly URLs owns its routes.
- Security: routed pages preserve physical-page URL authorization; cover one
  allow and one deny integration case.
- Delivery: first routing, resolution, caching, redirects, and authorization;
  then mobile detection, view switching, and mobile-master selection.
- Identity: package and assembly `Rehost.AspNet.FriendlyUrls`; original public
  namespaces; no Microsoft strong-name identity imitation.
- Language: modern C# internally, nullable enabled, implicit usings disabled,
  warnings as errors, SDK-default language version, original public type shapes.
- Caching: preserve all three resolver caching modes and the original static
  default using existing `VirtualPathProvider` and `System.Web` cache APIs.
- Packaging: managed library only. The frozen sample owns the content previously
  injected by the original `Microsoft.AspNet.FriendlyUrls` meta-package.
- Framework comparison: targeted documented readings only when behavior remains
  ambiguous; no reusable golden/oracle harness.
