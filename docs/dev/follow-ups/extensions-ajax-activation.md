# Extensions scope

The T1+T2 activation planned here landed 2026-08-22 (see
[extensions-compatibility](../extensions-compatibility.md) for the shipped
profile and
[system-web-extensions-portability](../research/system-web-extensions-portability.md)
for the analysis that grounded it). What remains is deliberate backlog:

- **Enabled JSON application services** — the internal script services are
  compiled and mapped, and the disabled-by-default refusal is wire-pinned, but
  no scenario runs them enabled over membership/role/profile providers
  (login/logout/isLoggedIn, roles, profile get/set). Dropped from the landed
  story because provider configuration on the shared `PageLiveScenario` host
  risked disturbing its tenants and no new fixture-host process was to be
  added. Trigger: a real application enabling
  `<authenticationService>`/`<roleService>`/`<profileService>`, or a fixture
  design that keeps the shared host undisturbed.
- **Async error format under `customErrors` On** — only the Off shape is
  exercised; the On variant needs a fixture whose `web.config` differs from
  the shared host's, and the existing `body-customerrors` fixture cannot host
  `.aspx` (its `<handlers>` section keeps no page mapping). Same no-new-process constraint as above.
- **`LinqDataSource`** plus `ILinqToSql`/`LinqToSqlWrapper` —
  `System.Data.Linq` has no modern implementation; the referencesource pin
  carries 43k lines if vendoring is ever warranted. Trigger: a real
  application declaring the control.
- **WCF proxy codegen** (`Compilation/**`: `.svcmap`/`.datasvcmap` build
  providers, `WCFModel`, `ProxyGenerator.cs`) — Visual Studio designer
  machinery over cut `System.ServiceModel.Description` codegen;
  dotnet-svcutil is the replacement. Trigger: an application shipping raw
  `.svcmap` under `App_WebReferences`.
- **WCF-hosted application services** (`ApplicationServices/*.svc` hosts) —
  need `System.ServiceModel.Activation` (12.6k lines in the pin; CoreWCF is
  the modern precedent). The JSON route above covers the AJAX managers.
- **Client Services** (`ClientServices/**`) — Windows-desktop stack (wininet,
  `WindowsIdentity`, WinForms paths, OleDb); out of the portable contract.
- **Debug scripts and localized script satellites** — Framework's debug build
  is a code generator not in the drop, and no `ScriptLibrary` satellites are
  built; `ScriptMode.Auto`/invariant strings are the supported shape
  ([provenance](../provenance/system-web-extensions.md)).
- **Inline `ServiceReference` proxies** (`/js` for `.svc` endpoints) —
  compile but are unassessed; nothing serves a `.svc` to proxy.
