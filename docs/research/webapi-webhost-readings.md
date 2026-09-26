# Web API 2 host readings

Evidence for the `Rehost.AspNet.WebApi.WebHost` package. WA1-WA5 were
observed on IIS Express 10.0.26013 on `winbox`, .NET Framework 4.8.9344,
2026-09-26.

## Method

One site on port 8131 with the shipped `Microsoft.AspNet.WebApi.WebHost`
5.3.0 graph in `bin` (`System.Web.Http.WebHost` 5.3.0, `System.Web.Http`
5.3.0, `System.Net.Http.Formatting` 6.0.0, Newtonsoft.Json 13.0.3 and the
`net45` dependencies of the client package). `Global.asax` swaps
`HttpControllerRouteHandler._instance` by reflection for a
`SessionHttpControllerRouteHandler` whose handler implements
`IRequiresSessionState`, then calls `GlobalConfiguration.Configure` with one
`MapHttpRoute("DefaultApi", "api/{controller}/{id}")`. `App_Code` holds
`PingController` (`GET` answers `{ pong = true }` or throws on `?fail=1`;
`POST` echoes the bound model's `Name`) and `SessionController` (`GET` writes
`Session["probe"]` on `?set=` and answers its value). `web.config`: C# 5
`App_Code`, `customErrors mode="Off"`, `authentication mode="None"`, session
state default, and `System.Net.Http` added under `compilation/assemblies`,
without which `App_Code` fails to compile with CS0012 (the VS Web API template
adds the same line). Requests used on-box `curl.exe -s -i`.

Every response carried `Cache-Control: no-cache`, `Pragma: no-cache`,
`Expires: -1`, `Content-Type: application/json; charset=utf-8`,
`X-AspNet-Version`, `X-Powered-By: ASP.NET` and a `Content-Length`; the host
sets `HttpCacheability.NoCache` on every Web API response.

| ID | Stimulus | Observed result |
| --- | --- | --- |
| WA1 | `GET /api/ping` | 200, body `{"pong":true}` (13 bytes). The controller was discovered from `App_Code` through `BuildManager.GetReferencedAssemblies` |
| WA2 | `POST /api/ping`, `Content-Type: application/json`, body `{"name":"x"}` | 200, body `{"echo":"x"}` (12 bytes) |
| WA3 | `GET /api/missing` | 404, body `{"Message":"No HTTP resource was found that matches the request URI 'http://localhost:8131/api/missing'.","MessageDetail":"No type was found that matches the controller named 'missing'."}` (187 bytes). Web API's JSON, not the ASP.NET 404 page |
| WA4 | `GET /api/session?set=v`, then `GET /api/session` with the cookie, then `GET /api/session` without | 200 `{"value":"v"}` with `Set-Cookie: ASP.NET_SessionId=<id>; path=/; HttpOnly; SameSite=Lax`; 200 `{"value":"v"}` with no `Set-Cookie`; 200 `{"value":null}` with no `Set-Cookie`. The reflective swap succeeded and the handler saw session state |
| WA5 | `GET /api/ping?fail=1` from localhost | 500, body `{"Message":"An error has occurred.","ExceptionMessage":"probe-failure","ExceptionType":"System.InvalidOperationException","StackTrace":"   at PingController.Get(String fail)\r\n   at System.Web.Http.Controllers.ReflectedHttpActionDescriptor..."}` (1569 bytes). Web API's JSON `HttpError`, not the ASP.NET error page; detail included because `customErrors` is `Off`. A remote client was not measured |
