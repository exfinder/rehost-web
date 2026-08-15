# WebFormsIdentityApplication dependency gaps

What the Visual Studio 2022 "Web Forms + Individual User Accounts" template
([`apps/WebFormsIdentityApplication`](../../apps/WebFormsIdentityApplication/README.md))
needs beyond the plain template already running on the port
([`apps/WebFormsApplication`](../../apps/WebFormsApplication/README.md)), and
where each need stands. Directions in the last column are **proposals**, not
decisions; nothing here is in `backlog.md` or `ROADMAP.md` yet.

## Result in one paragraph

The app adds 14 NuGet packages, all pure managed and Apache-licensed. Its
System.Web asks are small and mostly already met: one dynamic `HttpModule`
registered from a `PreApplicationStartMethod` (the mechanism FriendlyUrls
already uses on the port), async pipeline events at two stages that exist in
the classic pipeline, `MachineKey.Protect/Unprotect`, URL authorization, and
`HttpContext.User` being settable. The two genuinely non-portable pieces are
**LocalDb** (Windows-only database engine, so the connection string must
change) and the **`Microsoft.Owin.Host.SystemWeb` binary**, which binds to
Microsoft's strong-named `System.Web` and so must be recompiled against the
port, like every other System.Web consumer here. Everything else — OWIN core,
cookie authentication, ASP.NET Identity, Entity Framework 6.4 — should load
and run as shipped once those two are handled. Rough size: one recompiled
package (small), one storage decision (small code, real design), then a
long tail of "unassessed" System.Web surface the pages will light up.

## The mental model

Login on this template is a stack; each layer only talks to its neighbours:

```text
Account/*.aspx.cs ─ Context.GetOwinContext().GetUserManager<ApplicationUserManager>()
      │
      ▼
ASP.NET Identity 2.2 ─ UserManager / SignInManager / IdentityUser (Identity.Core, Identity.Owin)
      │                        │
      │                        ▼
      │                 Identity.EntityFramework ─ UserStore<T> ─ IdentityDbContext
      │                        │
      │                        ▼
      │                 EntityFramework 6.4 ─ System.Data.SqlClient ─ (LocalDb)\MSSQLLocalDB  ◄── Windows-only
      ▼
OWIN (Microsoft.Owin, .Security, .Security.Cookies) ─ per-request IOwinContext, cookie middleware, IDataProtector
      │
      ▼
Microsoft.Owin.Host.SystemWeb ─ OwinHttpModule (registered by PreApplicationStart) ◄── binds Microsoft System.Web
      │      • runs the OWIN pipeline inside HttpApplication events
      │      • IDataProtector = MachineKey.Protect/Unprotect
      │      • copies OWIN identity into HttpContext.User
      ▼
System.Web classic pipeline (the port) ─ UrlAuthorizationModule, MachineKey, LoginView/LoginStatus
```

Three questions decide everything:

| Question | Answer |
| --- | --- |
| Where do users live? | A SQL Server database EF creates on first use. On the template that is LocalDb, which exists only on Windows. |
| Who says "you are logged in"? | Katana's cookie middleware, not Forms authentication (`<authentication mode="None">`). It reads/writes `.AspNet.ApplicationCookie`, protected with `MachineKey.Protect`. |
| How does OWIN get into a Web Forms request? | `Microsoft.Owin.Host.SystemWeb` registers an `IHttpModule` at pre-application-start and hooks `AddOnAuthenticateRequestAsync` / `AddOnPreRequestHandlerExecuteAsync`. |

## What the app actually does

Framework baseline, IIS Express 10 on winbox, disposable copy of the frozen
WAP, database renamed per run, 2026-08-15 (`Set-Cookie` values elided):

| Step | Observed |
| --- | --- |
| `GET /` | 200; `Set-Cookie: __AntiXsrfToken=…; path=/; HttpOnly` (Site.Master code, not Identity) |
| `GET /Account/Manage` anonymous | 302 → `http://localhost:8181/Account/Login?ReturnUrl=%2FAccount%2FManage` (UrlAuthorization 401 from `Account/Web.config`, turned into a redirect by the cookie middleware; absolute `Location`) |
| `GET /Account/Register` | 200; form fields `ctl00$MainContent$Email/Password/ConfirmPassword`, submit `ctl00$MainContent$ctl08` |
| `POST /Account/Register` | 302 → `/`; ~3–6 s on first run (database created); `Set-Cookie` clears `.AspNet.TwoFactorCookie`, `.AspNet.ExternalCookie`, sets `.AspNet.ApplicationCookie` (491 chars, HttpOnly, path=/); `App_Data/<db>.mdf` + `_log.ldf` appear (8 MB each) |
| `GET /Account/Manage` signed in | 200; page reads `Hello, alice@example.com !` |
| `GET /` signed in | 200; `LoginView` shows the logged-in template, `LoginStatus` renders `__doPostBack('ctl00$ctl14$ctl02$ctl00','')` |
| `POST /` log off | 302 → `/`; `Set-Cookie: .ASPXAUTH=; expires=1999…; SameSite=Lax` **and** `.AspNet.ApplicationCookie=; expires=1970` — `LoginStatus` calls `FormsAuthentication.SignOut()` even under `mode="None"`, then the page's `OnLoggingOut` calls OWIN `SignOut` |
| `GET /Account/Manage` after log off | 302 → login again |
| `POST /Account/Login` wrong password | 200; body contains `Invalid login attempt` |
| `POST /Account/Login` right password | 302 → `/`; same three-cookie `Set-Cookie` as register |

Not exercised by the template as generated: external providers (all four
`app.Use…Authentication` calls are commented out), e-mail/SMS (stub services),
account confirmation and password reset (links commented out), two-factor.
Their assemblies still load and their pages still compile.

## Component map

"Ported" means a `Rehost.*` package exists. "Runs off Windows" is about the
shipped binary as-is on .NET 10 (`net45` assemblies load under `NU1701`).

| Component (packages.config) | What the app touches | Ported | Runs off Windows as shipped | Proposed direction | Size |
| --- | --- | --- | --- | --- | --- |
| `Microsoft.Owin` 4.2.2, `Owin` 1.0 | `IAppBuilder`, `IOwinContext`, `PathString`, `OwinStartupAttribute` | No | Yes — pure managed, no System.Web reference | Consume as-is from nuget.org | — |
| `Microsoft.Owin.Security` 4.2.2, `.Security.Cookies` | `UseCookieAuthentication`, `CookieAuthenticationProvider`, `IAuthenticationManager` (`SignOut`, `Challenge`, `GetExternalLoginInfo`) | No | Mostly — but its *default* `IDataProtectionProvider` is DPAPI (`ProtectedData`), which throws off Windows; only the SystemWeb host swaps in `MachineKey` | Consume as-is; the recompiled host must keep supplying the MachineKey protector | — |
| `Microsoft.Owin.Host.SystemWeb` 4.2.2 | `HttpContext.GetOwinContext()`, `OwinHttpModule`, `MachineKeyDataProtector`, `HttpContext.User` bridging | No | **No** — references Microsoft's `System.Web` 4.0.0.0 by strong name; also `Microsoft.Web.Infrastructure` | **Port**: `Rehost.WebForms.Owin.Host` recompiled from Katana source against `Rehost.WebForms.Runtime`, dropping `Microsoft.Web.Infrastructure` exactly as the Optimization port did (`docs/provenance/aspnet-web-optimization.md`) | S–M (~40 files; the module, call context, environment dictionary, data protector) |
| `Microsoft.Owin.Security.OAuth`, `.Google`, `.Facebook`, `.Twitter`, `.MicrosoftAccount` | Referenced; not called (commented out) | No | Yes | Consume as-is; behaviour unassessed until an app enables one | — |
| `Microsoft.AspNet.Identity.Core` 2.2.4 | `UserManager<T>`, `IdentityResult`, `PasswordValidator`, token providers, `IIdentityMessageService` | No | Yes — pure managed | Consume as-is | — |
| `Microsoft.AspNet.Identity.Owin` 2.2.4 | `SignInManager`, `CreatePerOwinContext`, `GetUserManager<T>`, `SecurityStampValidator`, `DataProtectorTokenProvider`, `IdentityFactoryOptions.DataProtectionProvider` | No | Yes — depends on Owin.Security.*, not System.Web | Consume as-is | — |
| `Microsoft.AspNet.Identity.EntityFramework` 2.2.4 | `IdentityDbContext<T>`, `IdentityUser`, `UserStore<T>` | No | Yes — depends on EF ≥ 6.1 | Consume as-is | — |
| `EntityFramework` 6.4.4 (+ `EntityFramework.SqlServer`) | `DbContext("DefaultConnection")`, code-first create, `entityFramework` config section, `providers` | No | Yes — 6.3+ ships `netstandard2.1`; SqlServer provider uses `System.Data.SqlClient`, which the runtime already depends on (`Directory.Packages.props`) | Consume as-is. **Open point**: EF reads `<connectionStrings>` and `<entityFramework>` through static `System.Configuration.ConfigurationManager`. On Framework that sees `web.config` because ASP.NET makes it the AppDomain's config file; the port opens `web.config` through `WebConfigurationManager` (`ApplicationBootstrap.cs`), so `ConfigurationManager` sees the host exe's config instead. Needs a bridge or a documented host-config placement | S code, one seam decision |
| LocalDb (`(LocalDb)\MSSQLLocalDB`, `AttachDbFilename=|DataDirectory|\…mdf`) | Database engine + file-attach on first `DbContext` use | n/a | **No** — LocalDb is a Windows-only SQL Server flavour | **Out of contract**: state the boundary; the app-visible fix is a connection string pointing at a reachable SQL Server (container/service). `|DataDirectory|` itself is fine — the port sets it to `App_Data` (`HttpRuntime.SetUpDataDirectory`) | S code, one design note |
| `Microsoft.Web.Infrastructure` 2.0 | Only as Katana's dependency (`DynamicModuleUtility`); Katana 4.x calls `HttpApplication.RegisterModule` directly | No | No — binds Microsoft System.Web | Drop in the recompiled host (precedent: Optimization) | — |
| `Newtonsoft.Json` 13 | Katana dependency | n/a | Yes | Consume as-is | — |
| Antlr, WebGrease, Optimization, FriendlyUrls, ScriptManager.*, DotNetCompilerPlatform, bootstrap/jQuery/Modernizr | Same as `WebFormsApplication` | Yes | — | Already handled by the existing App/Host pair | — |

## System.Web surface the app newly reaches

The port already runs `WebFormsApplication`; this app additionally needs the
following from `Rehost.WebForms.Runtime`. Status from `docs/compatibility.md`.

| Need | Who needs it | Status on the port |
| --- | --- | --- |
| `PreApplicationStartMethod` scan + `HttpApplication.RegisterModule` | Katana `PreApplicationStart.Initialize` | Proven — FriendlyUrls registers its redirect module the same way and runs in `WebFormsApplication` |
| `AddOnAuthenticateRequestAsync`, `AddOnPreRequestHandlerExecuteAsync`, `AddOnEndRequestAsync` | Katana stage segments (cookie auth marks `Authenticate`; unmarked middleware runs at `PreRequestHandlerExecute`) | Classic pipeline has both events (`HttpApplication.BuildSteps`). Note: classic mode has **no** `MapRequestHandler` event; Katana would silently skip a segment marked for that stage — irrelevant to this app, worth a diagnostic later |
| `HttpContext.Items["owin.Environment"]`, `HttpContext.User = …`, `Thread.CurrentPrincipal` | `GetOwinContext()`, identity bridging | Compiled; user-principal assignment unassessed |
| `MachineKey.Protect/Unprotect` with purposes | `MachineKeyDataProtector` (`.AspNet.ApplicationCookie`, Identity tokens) | Supported (`Protect`/`Unprotect`); auto-generated keys are process-scoped, so cookies die on restart unless `<machineKey>` is explicit — the app declares none |
| `HttpRequest.ServerVariables[REMOTE_ADDR/REMOTE_PORT/LOCAL_ADDR/SERVER_PORT/SERVER_PROTOCOL]`, `HttpResponse.BufferOutput = false`, `HttpResponseBase.AddOnSendingHeaders` | OWIN environment dictionary | Unassessed as a set |
| `HostingEnvironment.SiteName + ApplicationID`, `HttpRuntime.IISVersion` | Katana `AppName`, IIS 8 detection | `IISVersion` is `null` off IIS — fine, Katana guards it |
| `UrlAuthorizationModule` + per-folder `<location>` `<authorization>` | `Account/Web.config` protects `Manage.aspx` | Supported (direct and Friendly URLs) |
| `<authentication mode="None">` | Root `web.config` | Compiled; the mode itself is a no-op; Forms/roles remain unassessed |
| `LoginView`, `LoginStatus`, `FormsAuthentication.SignOut()`/`RequireSSL` | Site.Master | Compiled, unassessed. Baseline shows `SignOut()` writes an expired `.ASPXAUTH` even under `mode="None"` — a good parity check |
| `<sessionState mode="InProc" customProvider=…>` naming `System.Web.Providers` (not in `packages.config`) | Root `web.config` | Ignored under `InProc` on Framework; the port has no session module registered anyway (unassessed). Config parse must tolerate the unresolvable provider type |
| `<system.webServer><modules><remove name="FormsAuthentication"/>` | Root `web.config` | Unassessed / not translated — harmless, module is not registered |
| `<runtime><assemblyBinding>`, `<system.codedom>` | Root `web.config` | Already stripped by `Web.Rehost.config` XDT for the first app |
| `Response.StatusCode = 401; Response.End()` then OWIN challenge → 302 | `OpenAuthProviders.ascx` | Only with an external provider enabled — not exercised |

## Proposed order (proposal)

1. **Storage boundary first, on Windows**: point `DefaultConnection` at a SQL
   Server reachable from all three OSes and confirm EF 6.4 + Identity.EF work
   on .NET 10 with the shipped binaries. No port work; answers "does the data
   layer even run?" and settles the LocalDb boundary text.
2. **`Rehost.WebForms.Owin.Host`**: recompile Katana's SystemWeb host against
   the runtime (provenance record, `Microsoft.Web.Infrastructure` dropped,
   `MachineKey` protector kept). Unit-test the module registration and the
   two stage hooks; scenario-test register → login → log off against the
   baseline table above.
3. **App/Host pair** for this app: `packages.config` → package references
   (`Rehost.WebForms.Owin.Host` replaces `Microsoft.Owin.Host.SystemWeb`;
   everything else from nuget.org), connection string via `Web.Rehost.config`.
4. Classify what lights up: `LoginView`/`LoginStatus`, `FormsAuthentication`
   statics, `HttpContext.User`, ServerVariables, `<sessionState>` tolerance —
   each becomes a compatibility row or a backlog item.

## Sources

- Frozen app: `apps/WebFormsIdentityApplication/WebFormsIdentityApplication`
  (`packages.config`, `Web.config`, `App_Start/*.cs`, `Models/IdentityModels.cs`,
  `Account/*.aspx.cs`, `Site.Master.cs`).
- Katana (`Microsoft.Owin.Host.SystemWeb`, `main`): `PreApplicationStart.cs`,
  `OwinHttpModule.cs`, `IntegratedPipeline/IntegratedPipelineContext.cs`,
  `OwinCallContext.cs`, `OwinCallContext.Environment.cs`, `OwinAppContext.cs`,
  `DataProtection/MachineKeyDataProtector.cs`.
- Port: `src/System.Web.ReferenceSource/HttpApplication.cs` (`BuildSteps`,
  `RegisterModule`), `HttpRuntime.cs` (`SetUpDataDirectory`, `IISVersion`),
  `src/Rehost.WebForms.FriendlyUrls/AssemblyInfo.cs`,
  `docs/compatibility.md`, `docs/provenance/aspnet-web-optimization.md`.
