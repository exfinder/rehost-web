# IIS integrated mode: modules and handlers readings

The evidence behind ledger P83, P85 and P86, and behind the `system.webServer/handlers`
and `/modules` rows of the [compatibility map](../compatibility.md). Thirty-eight readings
(MH1-MH38) taken against full IIS 10 on `winbox` in four rounds, 2026-08-22 and
2026-08-23, one throwaway application per case. Each reading states the configuration
fragment, the request, the response as `curl` printed it, and the conclusion drawn.

Readings that overturned a design assumption: MH1 (end-side module events are not
reversed), MH9v (a re-added inherited handler name does not regain top matching
priority), MH22 (handler types resolve lazily, module types do not), MH24 (a subfolder
`<modules>` section is ignored rather than refused), MH28 (handler paths match
case-insensitively while verbs do not), MH33/MH34 (the classic-section validator does not
reach inside `<location>` but does resolve `configSource`), MH35 (`managedHandler` is
decided once from the pre-rewrite URL), MH37 (`TRACE` is 501, not 200), and MH38
(`<customHeaders>` reach responses the application never produces).

Rig: winbox, full IIS 10.0, app pool `MhPool` (v4.0, Integrated), site `MhSite` port 8112,
one application per case under `C:/Users/sshuser/probes/mh-rig/apps/<case>`. Probe assembly
`MhProbes.dll` (LogA/LogB/LogC append `X-MH: <name>:<event>` headers on BeginRequest,
AuthenticateRequest, AcquireRequestState, PostRequestHandlerExecute, EndRequest;
HandlerA/HandlerB write `HANDLED-BY:<name>`). `probe.aspx` dumps
`Context.ApplicationInstance.Modules.AllKeys`. All requests via on-box
`curl.exe -s -i http://localhost:8112/<app>/...`. Date: 2026-08-22.

Unless noted, each app config also carries
`<system.web><compilation tempDirectory="C:\Users\sshuser\probes\mh-rig\aspnet-temp"/></system.web>`
(omitted from fragments below).

## MH1 — module order: two webServer/modules adds

Config:

```xml
<system.webServer>
  <validation validateIntegratedModeConfiguration="false" />
  <modules>
    <add name="LogA" type="MhProbes.LogA, MhProbes" />
    <add name="LogB" type="MhProbes.LogB, MhProbes" />
  </modules>
</system.webServer>
```

Request: `GET /mh1/probe.aspx`

Response:

```
HTTP/1.1 200 OK
X-MH: LogA:BeginRequest
X-MH: LogB:BeginRequest
X-MH: LogA:AuthenticateRequest
X-MH: LogB:AuthenticateRequest
X-MH: LogA:AcquireRequestState
X-MH: LogB:AcquireRequestState
X-MH: LogA:PostRequestHandlerExecute
X-MH: LogB:PostRequestHandlerExecute
X-MH: LogA:EndRequest
X-MH: LogB:EndRequest

MODULES:OutputCache|Session|WindowsAuthentication|FormsAuthentication|DefaultAuthentication|RoleManager|UrlAuthorization|FileAuthorization|AnonymousIdentification|Profile|UrlMappingsModule|UrlRoutingModule-4.0|ScriptModule-4.0|LogA|LogB
```

Conclusion: app-level adds land at the END of the effective managed module list (after all inherited defaults); same-event firing order = list order, and EndRequest is NOT reversed (still LogA then LogB).

## MH2 — remove/re-add "Session" (re-added type = LogC)

Config:

```xml
<system.webServer>
  <validation validateIntegratedModeConfiguration="false" />
  <modules>
    <remove name="Session" />
    <add name="Session" type="MhProbes.LogC, MhProbes" />
  </modules>
</system.webServer>
```

Request: `GET /mh2/probe.aspx`

Response:

```
HTTP/1.1 200 OK
X-MH: LogC:BeginRequest
X-MH: LogC:AuthenticateRequest
X-MH: LogC:AcquireRequestState
X-MH: LogC:PostRequestHandlerExecute
X-MH: LogC:EndRequest

MODULES:OutputCache|Session|WindowsAuthentication|FormsAuthentication|DefaultAuthentication|RoleManager|UrlAuthorization|FileAuthorization|AnonymousIdentification|Profile|UrlMappingsModule|UrlRoutingModule-4.0|ScriptModule-4.0
```

Conclusion: the type replacement took effect (LogC fired, once per event), and the name "Session" appears in Modules.AllKeys at its ORIGINAL inherited position (2nd), not at the end.

## MH3 — same name+type registered in BOTH httpModules and webServer/modules, flag false

Config:

```xml
<system.web>
  <httpModules>
    <add name="LogA" type="MhProbes.LogA, MhProbes" />
  </httpModules>
</system.web>
<system.webServer>
  <validation validateIntegratedModeConfiguration="false" />
  <modules>
    <add name="LogA" type="MhProbes.LogA, MhProbes" />
  </modules>
</system.webServer>
```

Request: `GET /mh3/probe.aspx`

Response:

```
HTTP/1.1 200 OK
X-MH: LogA:BeginRequest
X-MH: LogA:AuthenticateRequest
X-MH: LogA:AcquireRequestState
X-MH: LogA:PostRequestHandlerExecute
X-MH: LogA:EndRequest

MODULES:...|ScriptModule-4.0|LogA
```

Conclusion: runs exactly ONCE (single BeginRequest header, single AllKeys entry) — the httpModules copy does not double-register.

## MH4 — classic-only module (httpModules), flag false

Config:

```xml
<system.web>
  <httpModules>
    <add name="LogA" type="MhProbes.LogA, MhProbes" />
  </httpModules>
</system.web>
<system.webServer>
  <validation validateIntegratedModeConfiguration="false" />
</system.webServer>
```

Request: `GET /mh4/probe.aspx`

Response:

```
HTTP/1.1 200 OK
(no X-MH headers)

MODULES:OutputCache|Session|WindowsAuthentication|FormsAuthentication|DefaultAuthentication|RoleManager|UrlAuthorization|FileAuthorization|AnonymousIdentification|Profile|UrlMappingsModule|UrlRoutingModule-4.0|ScriptModule-4.0
```

Conclusion: an httpModules-only registration does NOT run at all in the integrated pool (no events, absent from Modules.AllKeys) — it is silently ignored once validation is suppressed.

## MH5 — validation element ABSENT + classic entries present

Three apps, all WITHOUT any `<validation>` element:

- mh5a: `<system.web><httpModules><add name="LogA" type="MhProbes.LogA, MhProbes"/></httpModules></system.web>`
- mh5b: `<system.web><httpHandlers><add verb="*" path="probe3.axd" type="MhProbes.HandlerA, MhProbes"/></httpHandlers></system.web>`
- mh5c: `<system.web><identity impersonate="true"/></system.web>`

Requests and responses (detailed-error fields extracted from full local error pages; every page's
h2 message is "An ASP.NET setting has been detected that does not apply in Integrated managed
pipeline mode."):

```
GET /mh5a/probe.aspx  -> HTTP/1.1 500 Internal Server Error
  HTTP Error 500.22 - Internal Server Error
  Module: ConfigurationValidationModule  Notification: BeginRequest
  Handler: PageHandlerFactory-Integrated-4.0  Error Code: 0x80070032

GET /mh5a/static.txt  -> HTTP/1.1 500 Internal Server Error
  HTTP Error 500.22, Module: ConfigurationValidationModule, Notification: BeginRequest
  Handler: StaticFile  Error Code: 0x80070032

GET /mh5b/probe.aspx  -> HTTP/1.1 500 Internal Server Error
  HTTP Error 500.23 - Internal Server Error
  Module: ConfigurationValidationModule  Notification: BeginRequest  Error Code: 0x80070032

GET /mh5c/probe.aspx  -> HTTP/1.1 500 Internal Server Error
  HTTP Error 500.24 - Internal Server Error
  Module: ConfigurationValidationModule  Notification: BeginRequest  Error Code: 0x80070032
```

Conclusion: with the flag absent, httpModules -> 500.22, httpHandlers -> 500.23, `<identity impersonate="true"/>` -> 500.24, all raised by ConfigurationValidationModule at BeginRequest with 0x80070032 — and the 500.22 fires for STATIC requests to the app too, not just managed ones.

## MH6 — validation flag explicitly TRUE + classic entries

Same classic entries as MH5a/MH5b plus `<validation validateIntegratedModeConfiguration="true"/>`.

```
GET /mh6a/probe.aspx  -> HTTP/1.1 500, HTTP Error 500.22 (httpModules variant)
  Module: ConfigurationValidationModule  Notification: BeginRequest  Error Code: 0x80070032
GET /mh6b/probe.aspx  -> HTTP/1.1 500, HTTP Error 500.23 (httpHandlers variant)
  Module: ConfigurationValidationModule  Notification: BeginRequest  Error Code: 0x80070032
```

Conclusion: explicit `true` behaves identically to the flag being absent (same 500.22/500.23, same module/notification/error code).

## MH7 — disagreeing duplicate: name "Mh" = LogA in httpModules, LogB in webServer/modules, flag false

Config:

```xml
<system.web>
  <httpModules>
    <add name="Mh" type="MhProbes.LogA, MhProbes" />
  </httpModules>
</system.web>
<system.webServer>
  <validation validateIntegratedModeConfiguration="false" />
  <modules>
    <add name="Mh" type="MhProbes.LogB, MhProbes" />
  </modules>
</system.webServer>
```

Request: `GET /mh7/probe.aspx`

Response:

```
HTTP/1.1 200 OK
X-MH: LogB:BeginRequest
X-MH: LogB:AuthenticateRequest
X-MH: LogB:AcquireRequestState
X-MH: LogB:PostRequestHandlerExecute
X-MH: LogB:EndRequest

MODULES:...|ScriptModule-4.0|Mh
```

Conclusion: only the webServer/modules type (LogB) runs; the httpModules type never fires — system.webServer/modules is the authoritative list in integrated mode.

## MH8 — handler matching: document order vs specificity, app adds vs inherited *.aspx

mh8a config (specific first):

```xml
<handlers>
  <add name="HA" path="probe2.aspx" verb="*" type="MhProbes.HandlerA, MhProbes" />
  <add name="HB" path="*.aspx" verb="*" type="MhProbes.HandlerB, MhProbes" />
</handlers>
```

mh8b config: same two adds in reverse order (HB `*.aspx` first, HA `probe2.aspx` second).
`probe.aspx` and `probe2.aspx` exist on disk in both apps; `other.aspx` does not.

```
GET /mh8a/probe2.aspx -> 200, body: HANDLED-BY:HandlerA
GET /mh8a/other.aspx  -> 200, body: HANDLED-BY:HandlerB
GET /mh8a/probe.aspx  -> 200, body: HANDLED-BY:HandlerB   (real page; PageHandlerFactory did NOT run)

GET /mh8b/probe2.aspx -> 200, body: HANDLED-BY:HandlerB
GET /mh8b/other.aspx  -> 200, body: HANDLED-BY:HandlerB
GET /mh8b/probe.aspx  -> 200, body: HANDLED-BY:HandlerB
```

Conclusion: among an app's own adds, FIRST MATCH IN DOCUMENT ORDER wins (wildcard listed first beats a later exact path — no specificity ranking), and app-level adds beat the inherited PageHandlerFactory `*.aspx` mapping even for a real .aspx file.

## MH9 — remove + re-add ExtensionlessUrlHandler-Integrated-4.0 ahead of an app mapping

Config:

```xml
<handlers>
  <remove name="ExtensionlessUrlHandler-Integrated-4.0" />
  <add name="ExtensionlessUrlHandler-Integrated-4.0" path="*." verb="*"
       type="System.Web.Handlers.TransferRequestHandler"
       preCondition="integratedMode,runtimeVersionv4.0" />
  <add name="ApiHandler" path="api" verb="*" type="MhProbes.HandlerA, MhProbes" />
</handlers>
```

```
GET /mh9/api        -> 200, body: HANDLED-BY:HandlerA
GET /mh9/probe.aspx -> 200, MODULES:... (normal page, no app modules — sanity OK)
```

Conclusion: `/api` (extensionless, matches both `*.` and `api`) is served by ApiHandler — the re-added inherited-name `*.` entry listed FIRST did not capture it, unlike MH8 where document order decided between the app's own fresh adds.

Follow-up: `GET /mh9/nosuchthing` (extensionless, no matching app add) -> 404.0, detailed error
shows `Module: IIS Web Core, Notification: MapRequestHandler, Handler: StaticFile, 0x80070002`.
Because TransferRequestHandler re-maps a child request (which skips the `*.` entry), neither the
/api nor the /nosuchthing reading can distinguish "ApiHandler matched directly" from
"TransferRequestHandler matched first, then the child request hit ApiHandler/StaticFile".
Disambiguating variant below (MH9v) replaces the re-added type with a marker handler.

## MH10 — preCondition="managedHandler" on LogA, none on LogB

Config:

```xml
<modules>
  <add name="LogA" type="MhProbes.LogA, MhProbes" preCondition="managedHandler" />
  <add name="LogB" type="MhProbes.LogB, MhProbes" />
</modules>
```

```
GET /mh10/probe.aspx -> 200
  X-MH: LogA:BeginRequest / LogB:BeginRequest / ... (both modules, all five events, A before B)
  MODULES:...|ScriptModule-4.0|LogA|LogB

GET /mh10/static.txt -> 200, body STATIC-OK, served by native StaticFile
  X-MH: LogB:BeginRequest
  X-MH: LogB:AuthenticateRequest
  X-MH: LogB:AcquireRequestState
  X-MH: LogB:PostRequestHandlerExecute
  X-MH: LogB:EndRequest
  (no LogA headers)
```

Conclusion: managedHandler-preconditioned module is skipped for the static request while the unconditioned managed module runs on it (all five events) — managed modules without the precondition DO run for native-handled static files in integrated mode.

## MH11 — preCondition="classicMode" in an integrated pool

Config: `<add name="LogA" type="MhProbes.LogA, MhProbes" preCondition="classicMode" />`

```
GET /mh11/probe.aspx -> 200, no X-MH headers, MODULES list = inherited defaults only (no LogA)
GET /mh11/static.txt -> 200, STATIC-OK, no X-MH headers
```

Conclusion: an unsatisfied (but valid) preCondition silently skips the module — no error, module absent from Modules.AllKeys.

## MH12 — invalid preCondition value "bogus"

Config: `<add name="LogA" type="MhProbes.LogA, MhProbes" preCondition="bogus" />`

```
GET /mh12/probe.aspx -> HTTP/1.1 500 Internal Server Error
  HTTP Error 500.0 - Internal Server Error
  Message: Module "LogA" has a bad precondition "bogus"
  Module: IIS Web Core  Notification: BeginRequest  Handler: Not yet determined
  Error Code: 0x8007000d
GET /mh12/static.txt -> same 500.0, same message/fields
```

Conclusion: an unrecognized preCondition token is a hard error — 500.0 from IIS Web Core (0x8007000d, "Module "LogA" has a bad precondition "bogus"") on EVERY request to the app, static included.

### MH9v — disambiguating variant: re-added name's type = marker HandlerB

Same config as MH9 but the re-added `ExtensionlessUrlHandler-Integrated-4.0` entry's type is
`MhProbes.HandlerB, MhProbes` (name/path/verb/preCondition unchanged, still listed before ApiHandler).

```
GET /mh9/api         -> 200, body: HANDLED-BY:HandlerA   (the LATER exact-path app add)
GET /mh9/nosuchthing -> 200, body: HANDLED-BY:HandlerB   (proves the re-added *. entry is active and matches extensionless URLs)
```

### MH9v2 — control: identical shape with FRESH names (no remove, no inherited name)

```xml
<handlers>
  <add name="DotWild" path="*." verb="*" type="MhProbes.HandlerB, MhProbes" />
  <add name="ApiExact" path="api" verb="*" type="MhProbes.HandlerA, MhProbes" />
</handlers>
```

```
GET /mh9/api         -> 200, body: HANDLED-BY:HandlerB   (the FIRST-listed *. wildcard wins)
GET /mh9/nosuchthing -> 200, body: HANDLED-BY:HandlerB
```

Conclusion (MH9 overall): a remove/re-add of an inherited handler name is active but matches AFTER the app's own fresh adds even when listed first (/api -> ApiHandler), whereas the identical pattern pair under fresh names follows document order (/api -> the first-listed `*.` entry) — the re-added inherited name does not regain top matching priority.

## MH13 — duplicate `<add>` with the same name in one webServer/modules

Config:

```xml
<modules>
  <add name="Dup" type="MhProbes.LogA, MhProbes" />
  <add name="Dup" type="MhProbes.LogB, MhProbes" />
</modules>
```

```
GET /mh13/probe.aspx -> HTTP/1.1 500 Internal Server Error
  HTTP Error 500.19 - Internal Server Error
  Module: IIS Web Core  Notification: BeginRequest  Handler: Not yet determined
  Error Code: 0x8007000d
  Config Error: Cannot add duplicate collection entry of type 'add' with unique key attribute 'name' set to 'Dup'
  Config File: \\?\C:\Users\sshuser\probes\mh-rig\apps\mh13\web.config
GET /mh13/static.txt -> same 500.19, same config error
GET /mh13/probe.aspx (repeat) -> 500 again
```

Conclusion: duplicate same-name add in one file is 500.19 (0x8007000d, "Cannot add duplicate collection entry ... 'Dup'") and fails EVERY request to the app (managed and static, consistently on repeat).

## MH14 — `<remove>` of an absent module name

Config:

```xml
<modules>
  <remove name="NoSuchModule" />
  <add name="LogA" type="MhProbes.LogA, MhProbes" />
</modules>
```

```
GET /mh14/probe.aspx -> 200
  X-MH: LogA:* (all five events)
  MODULES:...|ScriptModule-4.0|LogA
```

Conclusion: removing a nonexistent name is silently tolerated — no error, the sibling add works normally.

## MH15 — `<clear/>` in webServer/modules then add LogA only

Config:

```xml
<modules>
  <clear />
  <add name="LogA" type="MhProbes.LogA, MhProbes" />
</modules>
```

```
GET /mh15/probe.aspx -> HTTP/1.1 500 Internal Server Error
  HTTP Error 500.19 - Internal Server Error
  Module: IIS Web Core  Notification: BeginRequest  Handler: Not yet determined
  Error Code: 0x80070021
  Config Error: Lock violation
  Config Source highlights line 9: <clear />
GET /mh15/static.txt -> same 500.19 Lock violation
```

Conclusion: `<clear/>` at application level is rejected outright — 500.19 "Lock violation" (0x80070021) on every request (inherited module entries are locked), so no "cleared" pipeline is ever observable this way.

## MH16 — classic-only handler (httpHandlers) under integrated, flag false

Config:

```xml
<system.web>
  <httpHandlers>
    <add verb="*" path="probe3.axd" type="MhProbes.HandlerA, MhProbes" />
  </httpHandlers>
</system.web>
<system.webServer>
  <validation validateIntegratedModeConfiguration="false" />
</system.webServer>
```

```
GET /mh16/probe3.axd -> HTTP/1.1 404 Not Found
  HTTP Error 404.0 - Not Found
  Module: IIS Web Core  Notification: MapRequestHandler  Handler: StaticFile
  Error Code: 0x80070002
```

Conclusion: an httpHandlers-only mapping is never consulted in integrated mode — the request falls through to StaticFile and 404s (0x80070002), it is not served.

## Teardown

`Remove-Website MhSite` + `Remove-WebAppPool MhPool` executed (TEARDOWN-OK). Rig files left in
place at `C:/Users/sshuser/probes/mh-rig/` (mh9's web.config restored to the MH9 fragment).
No firewall rules were added.

# Round 2 (MH17–MH27) — same rig, pool/site recreated 2026-08-22

## MH17 — runAllManagedModulesForAllRequests="true" with MH10's module pair

Config:

```xml
<modules runAllManagedModulesForAllRequests="true">
  <add name="LogA" type="MhProbes.LogA, MhProbes" preCondition="managedHandler" />
  <add name="LogB" type="MhProbes.LogB, MhProbes" />
</modules>
```

```
GET /mh17/probe.aspx -> 200, both LogA and LogB fire all five events, MODULES:...|LogA|LogB
GET /mh17/static.txt -> 200, STATIC-OK, and BOTH LogA and LogB fire all five events:
  X-MH: LogA:BeginRequest / LogB:BeginRequest / ... / LogA:EndRequest / LogB:EndRequest
```

Conclusion: runAllManagedModulesForAllRequests="true" makes the managedHandler-preconditioned module (LogA) run on the static request too (contrast MH10, where LogA was skipped).

## MH18 — `<clear/>` in webServer/handlers then one add

Config:

```xml
<handlers>
  <clear />
  <add name="HA" path="probe2.aspx" verb="*" type="MhProbes.HandlerA, MhProbes" />
</handlers>
```

```
GET /mh18/probe2.aspx -> 200, body: HANDLED-BY:HandlerA
GET /mh18/probe.aspx  -> HTTP/1.1 404 Not Found
  HTTP Error 404.4 - Not Found
  Module: IIS Web Core  Notification: MapRequestHandler  Handler: Not yet determined
  Error Code: 0x80070002
GET /mh18/static.txt  -> same 404.4 (Handler: Not yet determined, 0x80070002)
```

Conclusion: handlers `<clear/>` IS legal at app level (unlike modules, MH15's lock violation) — inherited PageHandlerFactory/StaticFile vanish and any unmatched URL gets 404.4 "no handler determined" at MapRequestHandler; the surviving add still works.

## MH19 — duplicate `<add name="Dup">` in webServer/handlers (different paths)

```
GET /mh19/dup1.aspx  -> HTTP/1.1 500, HTTP Error 500.19
  Module: IIS Web Core  Notification: BeginRequest  Error Code: 0x8007000d
  Config Error: Cannot add duplicate collection entry of type 'add' with unique key attribute 'name' set to 'Dup'
GET /mh19/static.txt -> same 500.19
```

Conclusion: duplicate handler name is the same 500.19/0x8007000d shape as MH13's duplicate module name, and fails every request including static.

## MH20 — handlers `<remove>` of an absent name

Config: `<remove name="NoSuchHandler" />` + `<add name="HA" path="probe2.aspx" ...HandlerA... />`

```
GET /mh20/probe2.aspx -> 200, body: HANDLED-BY:HandlerA
```

Conclusion: removing a nonexistent handler name is silently tolerated (like MH14 for modules).

## MH21 — verb semantics on handler mappings

mh21a config:

```xml
<handlers>
  <add name="P" path="probe2.aspx" verb="POST" type="MhProbes.HandlerA, MhProbes" />
  <add name="W" path="*.aspx" verb="*" type="MhProbes.HandlerB, MhProbes" />
</handlers>
```

mh21b config: ONLY `<add name="P" path="onlypost" verb="POST" type="MhProbes.HandlerA, MhProbes" />`

```
GET  /mh21a/probe2.aspx -> 200, body: HANDLED-BY:HandlerB   (verb-mismatched exact entry skipped, falls to catch-all)
POST /mh21a/probe2.aspx (Content-Length: 0) -> 200, body: HANDLED-BY:HandlerA
GET  /mh21b/onlypost -> HTTP/1.1 404 Not Found
  HTTP Error 404.0 - Not Found
  Module: IIS Web Core  Notification: MapRequestHandler  Handler: StaticFile  Error Code: 0x80070002
POST /mh21b/onlypost (Content-Length: 0) -> 200, body: HANDLED-BY:HandlerA
(note: POST with neither body nor Content-Length is rejected earlier with a bare "HTTP Error 411. The request must be chunked or have a content length." — http.sys, before any handler mapping)
```

Conclusion: a verb-mismatched mapping is simply skipped during matching — GET falls through to the next match (catch-all in mh21a, inherited StaticFile -> 404.0 in mh21b); no 405 is produced by the mapping layer.

## MH22 — nonexistent type: failure timing

mh22a (handler): `<add name="Bad" path="bad.spx" verb="*" type="No.Such.Type, NoAsm" />` plus
`<add name="HA" path="probe2.aspx" ...HandlerA... />`.

```
GET /mh22a/probe2.aspx -> 200, HANDLED-BY:HandlerA          (good mapping unaffected)
GET /mh22a/probe.aspx  -> 200, MODULES:... (normal page)    (rest of app unaffected)
GET /mh22a/bad.spx     -> HTTP/1.1 500 Internal Server Error (ASP.NET error page)
  System.IO.FileNotFoundException: Could not load file or assembly 'NoAsm' or one of its
  dependencies. The system cannot find the file specified.
```

mh22b (module): `<modules><add name="BadM" type="No.Such.Type, NoAsm" /></modules>`

```
GET /mh22b/probe.aspx -> HTTP/1.1 500 (ASP.NET error page), exception chain:
  [FileNotFoundException: Could not load file or assembly 'NoAsm' ...]
  [ConfigurationErrorsException: Could not load file or assembly 'NoAsm' ...]
  [HttpException (0x80004005): Could not load file or assembly 'NoAsm' ...]
GET /mh22b/static.txt -> same 500, same exception chain (static requests fail too)
```

Conclusion: a bad handler type is LAZY (only the mapped URL 500s, FileNotFoundException at first dispatch) while a bad module type kills EVERY request to the app including static (managed 500 with ConfigurationErrorsException/HttpException 0x80004005 chain).

## MH23 — what trips 500.22 with the validation flag ABSENT

mh23a: only `<system.web><httpModules><remove name="Session"/></httpModules></system.web>`
mh23b: only `<system.web><httpModules><clear/></httpModules></system.web>`

```
GET /mh23a/probe.aspx -> HTTP/1.1 500, HTTP Error 500.22
  Module: ConfigurationValidationModule  Notification: BeginRequest  Error Code: 0x80070032
GET /mh23b/probe.aspx -> identical 500.22
```

Conclusion: ANY app-level httpModules content — even a bare `<remove>` or `<clear/>` with no adds — trips 500.22 when the flag is absent.

## MH24 — `<modules>` add in a SUBFOLDER web.config

App root: validation false only. `sub/web.config`:

```xml
<system.webServer>
  <modules>
    <add name="LogA" type="MhProbes.LogA, MhProbes" />
  </modules>
</system.webServer>
```

```
GET /mh24/sub/probe.aspx -> 200, NO X-MH headers, MODULES list = inherited defaults only (no LogA)
GET /mh24/probe.aspx     -> 200, identical (root unaffected)
```

Conclusion: a folder-level `<modules>` add is silently ignored — no error, module never runs even for requests inside that folder, app root unaffected.

## MH25 — preConditions on handler adds

mh25a: `<add name="PC" path="probe2.aspx" verb="*" ...HandlerA... preCondition="classicMode" />`
then `<add name="W" path="*.aspx" verb="*" ...HandlerB... />`.

```
GET /mh25a/probe2.aspx -> 200, body: HANDLED-BY:HandlerB
```

mh25b: only `<add name="PC" path="probe2.aspx" ...HandlerA... preCondition="bogus" />`.

```
GET /mh25b/probe2.aspx -> HTTP/1.1 500, HTTP Error 500.0
  Message: Handler "PC" has a bad precondition "bogus"
  Module: IIS Web Core  Notification: BeginRequest  Handler: Not yet determined  Error Code: 0x8007000d
GET /mh25b/static.txt  -> same 500.0
```

Conclusion: an unsatisfied valid handler preCondition just removes the entry from matching (falls to the next match), while an invalid token is the same hard 500.0/0x8007000d shape as MH12, only with "Handler" instead of "Module" in the message, failing every request.

## MH26 — resourceType and a mapped file that does not exist on disk

mh26a: `<add name="Ghost" path="ghost.aspx" verb="*" type="MhProbes.HandlerA, MhProbes" />`
(default resourceType; ghost.aspx NOT on disk).

```
GET /mh26a/ghost.aspx -> 200, body: HANDLED-BY:HandlerA
```

mh26b: same add plus `resourceType="File"`.

```
GET /mh26b/ghost.aspx -> HTTP/1.1 404 Not Found
  HTTP Error 404.0 - Not Found
  Module: IIS Web Core  Notification: MapRequestHandler  Handler: Ghost  Error Code: 0x80070002
```

Conclusion: default resourceType (Unspecified) serves the mapping with no file on disk; resourceType="File" makes the same mapping 404.0 (0x80070002) — and the error page still names "Ghost" as the mapped handler.

## MH27 — cross-level handler ordering and child `<remove>` of a parent add

mh27a: app-root handlers `<add name="RootW" path="*.aspx" ...HandlerB... />`;
`sub/web.config` handlers `<add name="SubW" path="*.aspx" ...HandlerA... />`. Both cover *.aspx.

```
GET /mh27a/sub/probe.aspx -> 200, body: HANDLED-BY:HandlerA   (child's add wins)
GET /mh27a/probe.aspx     -> 200, body: HANDLED-BY:HandlerB
```

mh27b: same root; `sub/web.config` is only `<handlers><remove name="RootW" /></handlers>`.

```
GET /mh27b/sub/probe.aspx -> 200, body = MODULES:... (the PAGE executed — inherited PageHandlerFactory took over)
GET /mh27b/probe.aspx     -> 200, body: HANDLED-BY:HandlerB   (root still uses RootW)
```

Conclusion: for the same pattern, the deepest (child) handler add wins over the parent app-level add, and a child folder CAN `<remove>` a parent's app-level add — matching then falls back to the inherited PageHandlerFactory for that folder only.

## Teardown (round 2)

`Remove-Website MhSite` + `Remove-WebAppPool MhPool` executed. Rig files left at
`C:/Users/sshuser/probes/mh-rig/`. No firewall rules were added.

# Round 3 (MH28) — same rig, pool/site recreated 2026-08-23

## MH28 — handler path/verb matching: casing, multi-segment patterns, PathInfo

Four apps, each with only `<validation validateIntegratedModeConfiguration="false"/>` plus the
`<handlers>` fragment shown. `probe.aspx` exists on disk in each (and in `mh28c/sub/`);
`marker.axd`/`verbcase.axd` exist nowhere on disk.

```xml
<!-- mh28a --> <add name="UpperPat"  path="*.ASPX"        verb="*" type="MhProbes.HandlerA, MhProbes" />
<!-- mh28b --> <add name="Exact"     path="Marker.axd"    verb="*" type="MhProbes.HandlerA, MhProbes" />
<!-- mh28c --> <add name="SubExact"  path="sub/marker.axd" verb="*" type="MhProbes.HandlerA, MhProbes" />
<!-- mh28d --> <add name="LowerVerb" path="verbcase.axd"  verb="get" type="MhProbes.HandlerA, MhProbes" />
```

```
GET /mh28a/probe.aspx        -> 200, HANDLED-BY:HandlerA   (pattern *.ASPX matched a .aspx URL)
GET /mh28a/probe.ASPX        -> 200, HANDLED-BY:HandlerA
GET /mh28b/Marker.axd        -> 200, HANDLED-BY:HandlerA
GET /mh28b/marker.axd        -> 200, HANDLED-BY:HandlerA
GET /mh28b/MARKER.axd        -> 200, HANDLED-BY:HandlerA
GET /mh28c/sub/marker.axd    -> 200, HANDLED-BY:HandlerA
GET /mh28c/marker.axd        -> HTTP/1.1 404 Not Found
  HTTP Error 404.0, Module: IIS Web Core, Notification: MapRequestHandler,
  Handler: StaticFile, Error Code: 0x80070002
GET  /mh28d/verbcase.axd     -> HTTP/1.1 404 Not Found
  HTTP Error 404.0, Module: IIS Web Core, Notification: MapRequestHandler,
  Handler: StaticFile, Error Code: 0x80070002
get  /mh28d/verbcase.axd  (curl -X get) -> 200, HANDLED-BY:HandlerA

GET /mh28b/Marker.axd/extra  -> 200, HANDLED-BY:HandlerA
GET /mh28a/probe.aspx/extra  -> 200, HANDLED-BY:HandlerA
GET /mh28c/deeper/sub/marker.axd -> 200, HANDLED-BY:HandlerA   (empty folders, nothing on disk)
```

Conclusion: handler `path` matching is CASE-INSENSITIVE on both sides — an uppercase pattern
matches a lowercase URL and vice versa — while the `verb` list is compared CASE-SENSITIVELY
(`verb="get"` refuses a `GET` request and serves a literal `get` one). A `path` pattern may carry
directory segments and then matches as a segment-boundary SUFFIX of the path, not anchored to the
application root: `sub/marker.axd` matches `/app/sub/marker.axd` and `/app/deeper/sub/marker.axd`
but not `/app/marker.axd`. Matching runs against the script path with PathInfo split off:
`*.ASPX` and `Marker.axd` both match a URL that continues past them.

## Teardown (round 3)

`Remove-Website MhSite` + `Remove-WebAppPool MhPool` executed (TEARDOWN-OK). The mh28a-d app
folders remain under `C:/Users/sshuser/probes/mh-rig/apps/`. No firewall rules were added.

# Round 4 — request filtering, config reach, rewrite, authorization, verbs, headers

Taken 2026-08-23 against the same IIS 10 on `winbox`: site `Mh2Site` on port 8113, pool
`Mh2Pool` (v4.0, Integrated), one application per case under
`C:/readings/mh2-rig/apps/<case>`, each added with `appcmd add app` so that app-level
configuration is judged as such. Probe assembly `Mh2Probes.dll`: `LogA`/`LogB` append
`X-MH: <name>:<notification>:<Request.FilePath>` on `BeginRequest`,
`AuthenticateRequest`, `AcquireRequestState`, `PostRequestHandlerExecute` and
`EndRequest`; `Rewriter` calls `RewritePath` at `BeginRequest` from `appSettings`.
Requests over a raw socket, so the transcripts are the wire.

The applications live outside the ssh user's profile: IIS serves as the pool identity,
which has no traverse right into another user's profile, and every request there answers
500.19 `Cannot read configuration file due to insufficient permissions` rather than the
reading. Case `b4` reproduces MH23 as a standing control; a round where it does not answer
500.22 is measuring something other than what it claims.

Unless noted, each app config carries
`<system.web><compilation tempDirectory="C:\readings\mh2-rig\aspnet-temp"/></system.web>`
(omitted from fragments below), and no `validateIntegratedModeConfiguration` flag.

## MH29 — `allowUnlisted="false"` on `<fileExtensions>`

```xml
<system.webServer>
  <security><requestFiltering><fileExtensions allowUnlisted="false" /></requestFiltering></security>
</system.webServer>
```

```
GET /a1/static.txt -> HTTP/1.1 404, HTTP Error 404.7
  The request filtering module is configured to deny the file extension.  Handler: StaticFile
GET /a1/page.aspx  -> identical 404.7
GET /a1/sub        -> identical 404.7   (extensionless path, existing directory)
```

With `.txt` listed back in:

```xml
<fileExtensions allowUnlisted="false"><add fileExtension=".txt" allowed="true" /></fileExtensions>
```

```
GET /a2/static.txt -> 200, body STATIC-TXT
GET /a2/data.dat   -> 404.7
```

Conclusion: `allowUnlisted="false"` turns the section from a deny list into a real allow
list. Everything not carrying `allowed="true"` is refused — managed extensions and an
**extensionless path** included, so the rule is not "extension present in the URL" but
"extension, possibly empty, is on the list".

## MH30 — an application `add`, `remove` and `clear` of `<fileExtensions>`

```
a3: <add fileExtension=".dat" allowed="false" />
GET /a3/data.dat        -> 404.7, request filtering

a4: <remove fileExtension=".config" />
GET /a4/sub/other.config -> HTTP/1.1 404, HTTP Error 404.3, Module: StaticFileModule
GET /a4/web.config       -> HTTP/1.1 404, HTTP Error 404.8, hiddenSegments

a5: <clear />
GET /a5/source.cs        -> HTTP/1.1 404, HTTP Error 404.3, Module: StaticFileModule
```

Conclusion: an application amends the inherited list with the ordinary collection
semantics, `remove` and `clear` included. Un-denying an extension does **not** make the
file downloadable: with the filtering row gone the request reaches `StaticFileModule`,
which has no `staticContent` MIME map for it and answers 404.3. Two independent gates
cover source extensions, and `web.config` keeps its own 404.8 from `<hiddenSegments>`
whatever the deny list says.

## MH31 — a non-boolean `allowed` value

```xml
<fileExtensions><add fileExtension=".dat" allowed="flase" /></fileExtensions>
```

```
GET /a6/data.dat -> HTTP/1.1 500, HTTP Error 500.19, Error Code: 0x8007000d
  Config Error: The 'allowed' attribute is invalid.  Boolean must be either 'true' or 'false'
  Config File: \\?\C:\readings\mh2-rig\apps\a6\web.config
```

Conclusion: the schema's `type="bool"` is enforced. A typo is a configuration error naming
the attribute and the file, not a value that quietly reads as "allowed".

## MH32 — `<fileExtensions>` in a subfolder `web.config`

`sub/web.config` carries `<add fileExtension=".dat" allowed="false" />`; the app root
carries nothing.

```
GET /a7/sub/data.dat -> 404.7, request filtering
GET /a7/data.dat     -> 404.3, Module: StaticFileModule   (root unaffected)
```

Conclusion: request filtering is resolved per path like the rest of the configuration —
a folder section binds that folder's subtree and nothing above it. Contrast MH24, where a
folder `<modules>` section is ignored outright.

## MH33 — classic registrations inside `<location>`

```xml
<location path="admin">
  <system.web>
    <httpModules><add name="LogA" type="Mh2Probes.LogA, Mh2Probes" /></httpModules>
  </system.web>
</location>
```

```
GET /b1/page.aspx       -> 200, PAGE:/b1/page.aspx,       no X-MH headers
GET /b1/admin/page.aspx -> 200, PAGE:/b1/admin/page.aspx, no X-MH headers
```

Same shape with `<identity impersonate="true" />` instead:

```
GET /b3/page.aspx -> 200, no refusal
```

Control, the MH23 shape at the app root with no `<location>`:

```
GET /b4/page.aspx -> HTTP/1.1 500, HTTP Error 500.22, Error Code: 0x80070032
```

Conclusion: `ConfigurationValidationModule` does **not** reach inside `<location>`. A
classic registration written there trips nothing, and the module never runs — for requests
inside the location as much as outside it. The application silently loses it.

## MH34 — classic registrations behind `configSource`

```xml
<system.web><httpModules configSource="mods.config" /></system.web>
```

`mods.config` holds `<httpModules><add name="LogA" ... /></httpModules>`.

```
GET /b2/page.aspx -> HTTP/1.1 500, HTTP Error 500.22, Error Code: 0x80070032
```

Conclusion: the validator judges the section's **resolved** content, so `configSource`
does not hide a classic registration the way `<location>` does. MH33 and MH34 are the
same author intent with opposite outcomes.

## MH35 — `managedHandler` under a URL rewrite

`LogA` carries `preCondition="managedHandler"`, `LogB` does not, `Rewriter` calls
`RewritePath` at `BeginRequest`. Headers carry the live `Request.FilePath`.

```
c1: rewrite ~/report.pdf -> ~/gen.aspx
GET /c1/report.pdf -> 200, PAGE:/c1/gen.aspx
  X-MH: Rewriter:~/report.pdf->~/gen.aspx
  X-MH: LogB:BeginRequest:/c1/gen.aspx
  X-MH: LogB:AuthenticateRequest:/c1/gen.aspx
  X-MH: LogB:AcquireRequestState:/c1/gen.aspx
  X-MH: LogB:ExecuteRequestHandler:/c1/gen.aspx
  X-MH: LogB:EndRequest:/c1/gen.aspx
  (no LogA header on any event, although a managed page served the request)

c2: rewrite ~/gen.aspx -> ~/report.pdf
GET /c2/gen.aspx -> 200, body REPORT-PDF
  X-MH: Rewriter:~/gen.aspx->~/report.pdf
  X-MH: LogA:BeginRequest:/c2/report.pdf        X-MH: LogB:BeginRequest:/c2/report.pdf
  X-MH: LogA:AuthenticateRequest:...            X-MH: LogB:AuthenticateRequest:...
  X-MH: LogA:AcquireRequestState:...            X-MH: LogB:AcquireRequestState:...
  X-MH: LogA:ExecuteRequestHandler:...          X-MH: LogB:ExecuteRequestHandler:...
  X-MH: LogA:EndRequest:...                     X-MH: LogB:EndRequest:...
  (LogA on every event, although a static file served the request)

c3 controls, no Rewriter registered:
GET /c3/report.pdf -> 200, LogB only
GET /c3/gen.aspx   -> 200, LogA and LogB on all five
```

Conclusion: the condition is evaluated **once, from the URL as it arrived**, before
`BeginRequest`, and is never revisited. A rewrite changes which handler runs but not the
answer: c1's request is served by a page with the conditioned module absent throughout,
c2's is served by a static file with it present throughout. Every conditioned module in a
request therefore sees the same answer — MH10 could not distinguish this from a per-event
evaluation because nothing moved the path.

## MH36 — `<authorization>` against a static file, managed and native

```xml
d1: <location path="prot">
      <system.web><authorization><deny users="?" /></authorization></system.web>
    </location>
```

```
GET /d1/prot/secret.txt -> 200, body SECRET-TXT     (anonymous, served)
GET /d1/prot/page.aspx  -> HTTP/1.1 401
```

```xml
d2: <location path="prot">
      <system.webServer><security><authorization>
        <remove users="*" />
        <add accessType="Allow" roles="Administrators" />
      </authorization></security></system.webServer>
    </location>
```

```
GET /d2/prot/secret.txt -> HTTP/1.1 401, HTTP Error 401.2, Handler: StaticFile
GET /d2/prot/page.aspx  -> HTTP/1.1 401, HTTP Error 401.2
```

Conclusion: the two URL-authorization systems have different reach. `system.web`
`<authorization>` runs through the `managedHandler`-conditioned `UrlAuthorizationModule`
and therefore does not protect a natively served static file — a `<deny users="?" />`
folder hands its `.txt` to an anonymous client. The native
`system.webServer/security/authorization` section refuses both. An application that locked
a folder down expecting file protection had to use the native section.

## MH37 — PUT, DELETE, OPTIONS and TRACE

No verb configuration; the shipped `TRACEVerbHandler`/`OPTIONSVerbHandler`
(`ProtocolSupportModule`) and `StaticFile` rows only.

```
PUT     /e1/static.txt -> HTTP/1.1 405, Allow: GET, HEAD, OPTIONS, TRACE
                          HTTP Error 405.0, Module: StaticFileModule, Handler: StaticFile
DELETE  /e1/static.txt -> identical 405
OPTIONS /e1/static.txt -> HTTP/1.1 200, Allow: OPTIONS, TRACE, GET, HEAD, POST  (no body)
TRACE   /e1/static.txt -> HTTP/1.1 501, HTTP Error 501.0
                          "returned only when the HTTP verb is Trace and the
                           EnableTraceMethod registry value is not set to 1"
PUT     /e1/page.aspx  -> 405, Allow: GET, HEAD, OPTIONS, TRACE
OPTIONS /e1/page.aspx  -> 200, Allow: OPTIONS, TRACE, GET, HEAD, POST
TRACE   /e1/page.aspx  -> 501
```

A body-bearing verb sent without `Content-Length` is refused with 411 before any handler
is consulted, so the reading requires `Content-Length: 0`.

Conclusion: `OPTIONS` is answered by the native protocol handler with 200 and an `Allow`
header listing the server's verbs, identically for a static file and an `.aspx` — the page
does not run. `TRACE` is **501 by default**, gated on an `EnableTraceMethod` registry value
that is off in a stock install; it is not a 200. An unmatched verb on either row falls
through to `StaticFile`, which answers 405 with its own narrower `Allow`.

## MH38 — `<httpProtocol><customHeaders>` reach, and what CORS gets from it

App `f1`, no handler or module configuration:

```xml
<system.webServer>
  <httpProtocol>
    <customHeaders>
      <add name="Access-Control-Allow-Origin" value="*" />
      <add name="X-Content-Type-Options" value="nosniff" />
      <remove name="X-Powered-By" />
    </customHeaders>
  </httpProtocol>
</system.webServer>
```

Full response heads, nothing filtered:

```
GET /f1/page.aspx      -> 200  Cache-Control: private / Content-Type: text/html; charset=utf-8
                               Server: Microsoft-IIS/10.0 / X-AspNet-Version: 4.0.30319
                               Access-Control-Allow-Origin: * / X-Content-Type-Options: nosniff

GET /f1/static.txt     -> 200  Content-Type: text/plain / Last-Modified / Accept-Ranges: bytes
                               ETag: W/"c570f4a02733dd1:0" / Server: Microsoft-IIS/10.0
                               Access-Control-Allow-Origin: * / X-Content-Type-Options: nosniff

OPTIONS /f1/page.aspx  -> 200  Allow: OPTIONS, TRACE, GET, HEAD, POST
                               Public: OPTIONS, TRACE, GET, HEAD, POST
                               Access-Control-Allow-Origin: * / X-Content-Type-Options: nosniff
                               Content-Length: 0

OPTIONS /f1/static.txt -> 200  identical to the page

TRACE /f1/static.txt   -> 501  Access-Control-Allow-Origin: * / X-Content-Type-Options: nosniff
GET /f1/missing.aspx   -> 404  Access-Control-Allow-Origin: * / X-Content-Type-Options: nosniff
```

The same `OPTIONS` sent as a CORS preflight:

```
OPTIONS /f1/page.aspx
  Origin: https://example.test
  Access-Control-Request-Method: PUT
-> 200, byte-identical to the plain OPTIONS above: Allow, Public,
   Access-Control-Allow-Origin, X-Content-Type-Options.
   No Access-Control-Allow-Methods, no Access-Control-Allow-Headers, no Vary.
```

Conclusion: `<customHeaders>` is applied below the managed pipeline — the headers appear on a
natively served static file, on the `ProtocolSupportModule`'s `OPTIONS` and `TRACE` responses,
and on an IIS error page, none of which the application produces. `<remove>` reaches the
inherited defaults the same way: the golden's `X-Powered-By` row is gone from every response.

That reach is not CORS support. IIS does not read `Origin` or `Access-Control-Request-Method`
and answers a preflight exactly as it answers a bare `OPTIONS`, so a configured
`Access-Control-Allow-Origin` covers simple cross-origin requests and every preflighted one
fails for want of `Access-Control-Allow-Methods`. An application carrying this config had
working simple-request CORS and broken preflight on IIS too.

Two headers this reading adds to the `OPTIONS` picture of MH37, both hidden by the earlier
filtered transcript: IIS emits the obsolete `Public` alongside `Allow` with the same list, and
`X-AspNet-Version` appears on managed responses but not on natively served ones.

## Teardown (round 4)

`Mh2Site` and `Mh2Pool` are left standing on `winbox` for follow-up readings, with the
applications under `C:/readings/mh2-rig/apps/`. The rig scripts were not committed; the
fragments and layout above are the reproduction. No firewall rules were added.
