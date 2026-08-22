# Extensions compatibility

## Implemented surface

`Rehost.WebForms.Extensions` compiles the `System.Web.Extensions` closure a
frozen Web Forms application reaches, minus the exclusions below: the
full-page ScriptManager stack (`ScriptManager`, `UpdatePanel`,
`UpdateProgress`, `Timer`, script/service/history plumbing,
`PageRequestManager`'s delta responses), `ScriptModule` (async-postback error
formatting, `Response.Redirect` interception, page-method routing), the ASMX
JSON chain (`ScriptHandlerFactory`, `RestHandler`, client proxy generation,
`JavaScriptSerializer`), the internal JSON application services
(`Profile/ProfileService`, `Security/{Authentication,Role}Service`) behind
Framework's built-in `*_JSON_AppService.axd` mappings, the query stack
(`QueryExtender`, its expression family, `QueryableDataSource`, the
`DynamicData` contract types), and the `ListView`/`DataPager` control family.

The 13 release Microsoft AJAX scripts are generated from the imported `.jsa`
recipes with era-contemporary AjaxMin and embedded;
`MicrosoftAjaxTimer.js` is byte-identical to Framework's shipped assembly and
the set stays within one percent in aggregate
([provenance](provenance/system-web-extensions.md)). `ScriptResource.axd`
serves them with `Sys.Res` appended per request. Debug scripts and localized
satellites are absent: `ScriptMode.Auto` falls back to release and client
strings stay invariant.

The baseline configuration carries Framework's four registrations verbatim on
Rehost assembly names: the `system.web.extensions` section group,
`ScriptModule-4.0`, the `*_AppService.axd` handler, and the
`System.Web.UI.WebControls.Expressions` tag mapping.

Scenario evidence: `tests/Rehost.WebForms.Hosting.Tests/AjaxOverKestrelTests.cs`
over the shared page fixture (`ajax/Panel.aspx`, `ajax/Query.aspx`);
`AjaxScriptResourceTests` gates the generated scripts. The sample app's
`Ajax.aspx` passed one real-browser journey (async timer ticks and button
posts update the panel without reload, console clean) — a manual gate, not a
standing test. Partial rendering requires a recognized browser: the default
capability profile disables it, on Framework and here alike.

## Wire readings vs full IIS (2026-08-22)

The same fixture page served by IIS 10 / .NET Framework 4.8 on the Windows
validation host and by the port, eight responses diffed byte-for-byte with
volatile headers (`Date`, `Server`, `X-AspNet-Version`, `X-Powered-By`),
host:port, and the `__VIEWSTATE` family normalized:

- **Byte-identical**: the async-postback deltas — UpdatePanel refresh, Timer
  tick, error token (`error|500|<message>` on HTTP 200), the
  `AsyncPostBackErrorMessage` replacement, `pageRedirect` with its URL-encoded
  target — and the page-method JSON response, delta length prefixes included.
- **The GET page** is identical after additionally normalizing
  `Environment.NewLine` (client-script blocks emit CRLF on Windows, LF off it)
  and the `WebResource.axd`/`ScriptResource.axd` `d=`/`t=` tokens
  (machine-key encryption and assembly timestamps).
- **Exception text is runtime-owned**: the disabled-application-service 500
  matches on `Message` and `ExceptionType`; `StackTrace` differs in frame
  detail and newlines, as already recorded for ASMX faults.

## Excluded surface

- **`LinqDataSource` and `ILinqToSql`/`LinqToSqlWrapper`** — `System.Data.Linq`
  has no modern implementation. Absent, not stubbed; a page declaring the
  control fails compilation naming the missing type.
- **WCF proxy codegen (`Compilation/**`: `.svcmap`/`.datasvcmap` build
  providers, `WCFModel`, `ProxyGenerator.cs`)** — Visual Studio designer
  machinery over cut `System.ServiceModel.Description` codegen; dotnet-svcutil
  is the replacement.
- **WCF-hosted application services (`ApplicationServices/*.svc` hosts)** —
  need `System.ServiceModel.Activation`. The JSON siblings above cover the
  `*_JSON_AppService.axd` route; `.svc` endpoints are not served.
- **Client Services (`ClientServices/**`)** — the Windows-desktop stack
  (wininet P/Invokes, `WindowsIdentity`, WinForms paths, OleDb).
- **`PermaLink.cs`** (commented out upstream) and
  **`LinqDataSourceContextData.cs`** (duplicate type definition).

Enabling the application services (`<authenticationService enabled="true"/>`
and siblings over membership/role/profile providers) is compiled but
unassessed; the default-disabled refusal is what the wire reading pins.

Deviations from the imported tree are listed in
[provenance](provenance/system-web-extensions.md).

Implementation:
`src/Rehost.WebForms.Extensions`.
Remaining scope:
[Extensions](follow-ups/extensions-ajax-activation.md).
