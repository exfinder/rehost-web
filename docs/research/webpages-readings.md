# ASP.NET Web Pages readings

Evidence for the `Rehost.AspNet.WebPages` package. WP1-WP13 were observed on
IIS Express 10.0.26013 on `winbox`, .NET Framework 4.8 with `System.Web`
4.8.9344, integrated mode, 2026-09-28; WP13 on the same site with its
compilation cache cleared first.

## Method

One site on port 8472 with the shipped `Microsoft.AspNet.WebPages` 3.3.0
(`System.Web.WebPages`, `.Razor`, `.Deployment` and `System.Web.Helpers`),
`Microsoft.AspNet.Razor` 3.3.0 and `Microsoft.Web.Infrastructure` 1.0.0.0 in
`bin`, the version Web Pages references, so no binding redirect is needed.
`web.config` carries no `webpages:*` keys, as BlogEngine.NET 3.3.8 does not:
`targetFramework="4.8"` on `compilation` (with `debug="false"`) and
`httpRuntime`, `customErrors mode="Off"`. The pages:

- `_AppStart.cshtml` sets `AppState["started"]`.
- `hello.cshtml` uses `_Layout.cshtml`, which calls
  `RenderPage("~/_Footer.cshtml", "footer-arg")`. It prints a query value, an
  encoded string, `Html.Raw`, a loop, an `@helper`, an `@functions` method,
  `Html.TextBox("q", "v")`, `Href("~/x/y")`, the `AppState` value, `IsPost`,
  `UrlData` and `VirtualPath`.
- `admin/index.cshtml` (with the layout), `both/Default.aspx` beside
  `both/default.cshtml`, and `onlycshtml/default.cshtml`.
- `validate.cshtml` reads `Request.Form["x"]` and
  `Request.Unvalidated.Form["x"]`; `notread.cshtml` never reads the form.
- `broken.cshtml` (a C# error) and `unclosed.cshtml` (an unclosed `@foreach`).
- `off/web.config` sets `webpages:Enabled` to `false`, beside `off/page.cshtml`.
- `ps/_PageStart.cshtml` sets `PageData`, writes before and after
  `RunPage()`; `ps/page.cshtml` prints the value.
- `WidgetHost.aspx` renders `Widgets/Tags/widget.cshtml` from an `App_Code`
  helper the way BlogEngine.NET's `RazorHelpers.ParseRazor` does:
  `BuildManager.GetCompiledType`, then `ExecutePageHierarchy` with a
  `WebPageContext` whose Model is an anonymous type the page reads as
  `Model.Title`.

Requests used on-box `curl.exe -s -i`. Every response also carried IIS
Express's `Server`, `X-AspNet-Version`, `X-Powered-By` and `X-SourceFiles`
headers, left out below.

| ID | Stimulus | Observed result |
| --- | --- | --- |
| WP1 | `GET /hello.cshtml?name=X` | 200, `Content-Type: text/html; charset=utf-8`, `Cache-Control: private`, `X-AspNetWebPages-Version: 3.0`. The body is the layout around the page, then the footer with `footer-arg`: `Hello, X!`, `&lt;b&gt;encoded&lt;/b&gt;`, `<b>raw</b>`, `<li>1</li><li>2</li><li>3</li>`, `<b>HI</b>`, `42`, `<input id="q" name="q" type="text" value="v" />`, `/x/y`, `app-start-ran`, `False`, `UrlData` count 0, `VirtualPath` `~/hello.cshtml` |
| WP2 | `GET /hello`, `/Hello`, `/HELLO.CSHTML` | 200 each, the same page. `VirtualPath` follows the URL's casing: `~/hello.cshtml`, `~/Hello.cshtml`, `~/HELLO.CSHTML` |
| WP3 | `GET /hello/a/b` | 200, `hello.cshtml` with `UrlData` count 2, `a`, `b` |
| WP4 | `GET /admin/`, `/admin`, `/both/`, `/onlycshtml/` | `/admin/` and `/admin` both 200 with `admin/index.cshtml` inside the layout; no redirect adds the slash. `/both/` serves `default.cshtml`, not `Default.aspx`. `/onlycshtml/` serves `default.cshtml`. All carry `X-AspNetWebPages-Version: 3.0` |
| WP5 | `GET /_Layout.cshtml`, `/_Layout` | 404 each, the ASP.NET "The resource cannot be found." page, `[HttpException]: Files with leading underscores ("_") cannot be served.` |
| WP6 | `GET /missing.cshtml`, `/missing` | `/missing.cshtml`: 404, the ASP.NET page, thrown from `WebPageRoute.DoPostResolveRequestCache` with the message "Exception of type 'System.Web.HttpException' was thrown." `/missing`: 404.0 from IIS itself (`IIS Web Core`, `MapRequestHandler`, handler `StaticFile`, `0x80070002`) |
| WP7 | `POST /validate.cshtml` and `/notread.cshtml` with `x=<script>` | Both 200. `Request.Form["x"]` threw `HttpRequestValidationException`; `Request.Unvalidated.Form["x"]` answered `<script>`. A page that never reads the form is served normally |
| WP8 | `GET /broken.cshtml`, `/unclosed.cshtml` | 500 each, no `X-AspNetWebPages-Version`. `broken`: "Compilation Error", `CS1525: Invalid expression term ';'`, the `.cshtml` lines 1-4, the physical source path, and the generated C# with `#line` pragmas naming the `.cshtml`. `unclosed`: "Parser Error", "The foreach block is missing a closing "}" character. ...", lines 1-2, `Source File: /unclosed.cshtml Line: 1` |
| WP9 | `GET /off/page.cshtml`, `/off/page` | `.cshtml`: 403, "This type of page is not served.", `[HttpException]: Path '/off/page.cshtml' is forbidden.` from `HttpForbiddenHandler`. Extensionless: IIS 404.0 as in WP6 |
| WP10 | `GET /WidgetHost.aspx` | 200; the widget renders inside the Web Forms page with the anonymous-type Model's `Title`. No `X-AspNetWebPages-Version`: only a request Web Pages serves itself carries it |
| WP11 | `HEAD /hello.cshtml` | 200, `Content-Length: 534`, the same headers as WP1, no body |
| WP12 | `GET /ps/page` | 200: `start-before`, then the page with `fromstart=yes`, then `start-after` |
| WP13 | Compilation cache cleared, then `GET /HELLO.CSHTML` as the first request, then `/hello.cshtml`, `/HELLO.CSHTML` and `/ps/PAGE.CSHTML` | The first `/HELLO.CSHTML`: 500, "There is no build provider registered for the extension '.CSHTML'"; the registry of build providers Web Pages adds matches the extension case-sensitively. `/hello.cshtml` then 200, and `/HELLO.CSHTML` 200 once the page has compiled. `/ps/PAGE.CSHTML`, never requested before, answered 200: the folder's other pages compiled together with the lower-case extensions they carry on disk, and the upper-case path found that result |

## The port, same pages

The same pages ran on the port's scratch build of the three assemblies on
macOS arm64. Every status matched. The bodies of WP1-WP4, WP7, WP10 and WP12
matched byte for byte, including the `VirtualPath` casing, and every response
that carried `X-AspNetWebPages-Version: 3.0` on Framework carried it on the
port. Three differences, none of them Web Pages' own:

- WP6 `/missing.cshtml`: the message was "External component has thrown an
  exception." .NET 10's `ExternalException` substitutes that text when the
  message is `null`, where Framework's left `Exception`'s "Exception of type
  ... was thrown.". `HttpException` derives from it, and the imported
  `System.Web` throws one with a `null` message in more than twenty places.
  Fixed: the port now answers Framework's text
  ([ledger P114](../portability-ledger.md)).
- WP6 and WP9, extensionless: the port answers "404 - File or directory not
  found." where IIS Express sent its detailed 404.0 page to the local client.
- WP8 `broken`: the source path is the platform's, and the generated C# has
  no `Runtime Version:4.0.30319.42000` header line, so its listing is one line
  shorter. `System.CodeDom` 10 omits that line.
