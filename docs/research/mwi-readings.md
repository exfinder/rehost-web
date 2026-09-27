# Microsoft.Web.Infrastructure readings

Evidence for `Rehost.Web.Infrastructure`. MWI1-MWI5 were observed on IIS
Express 10.0.26013 on `winbox`, .NET Framework 4.8 with `System.Web`
4.8.9344, integrated mode, 2026-09-27.

## Method

One site on port 8471 with the shipped `Microsoft.Web.Infrastructure` 2.0.0
assembly in `bin`, beside a probe assembly compiled for `net48` against that
package. The probe assembly declares a `PreApplicationStartMethod`, supplies
the `Global.asax` base class, and serves every `*.probe` path through one
handler registered under `system.webServer/handlers`. `web.config`:
`targetFramework="4.8"` on `compilation` and `httpRuntime` (request validation
mode 4.5), `customErrors mode="Off"`. Requests used on-box `curl.exe`.

The public surface was read from the 2.0.0 assembly's metadata and confirmed
by compiling against it: `InfrastructureHelper.UnloadAppDomain()`,
`InfrastructureHelper.IsCodeDomDefinedExtension(string)`,
`HttpContextHelper.ExecuteInNullContext(Action)`,
`DynamicModuleUtility.RegisterModule(Type)`, and
`ValidationUtility.EnableDynamicValidation(HttpContext)`,
`IsValidationEnabled(HttpContext)` returning `bool?`, and
`GetUnvalidatedCollections(HttpContext, out Func<NameValueCollection>, out Func<NameValueCollection>)`.
The package's EULA forbids decompiling it; nothing here comes from its code.

| ID | Stimulus | Observed result |
| --- | --- | --- |
| MWI1 | `DynamicModuleUtility.RegisterModule` from the `PreApplicationStartMethod` with a module that adds a response header in `BeginRequest`; then with `null` and with `typeof(string)`; then from a request handler | The module ran (`X-Mwi-Module: 1` from the first response on). `null`: `ArgumentNullException`, `ParamName` `moduleType`. `typeof(string)`: `ArgumentException`, `ParamName` `moduleType`, "The type 'System.String' is not an IHttpModule." From a request: `InvalidOperationException`, "Cannot register a module after the application has been initialized." Each is the exception `HttpApplication.RegisterModule` throws for the same argument at the same time, message included |
| MWI2 | `InfrastructureHelper.IsCodeDomDefinedExtension` for 11 inputs | `.cs`, `.CS`, `.vb`, `.js`, `.h` and `cs` (no dot): `true`. `.cpp`, `.cshtml`, `.aspx` and the empty string: `false`. `null`: `ArgumentNullException`, `ParamName` `extension`. `CodeDomProvider.IsDefinedExtension` from `System.CodeDom` 10.0.10 on macOS gives the same answers except `.js` and `.h`, which are `false` |
| MWI3 | `POST /mwi3.probe?q=%3Cscript%3E` with form `x=<script>&safe=plain`: `IsValidationEnabled`, `EnableDynamicValidation`, `IsValidationEnabled` again, `GetUnvalidatedCollections`, both getters, `Request.Form`, `Request.QueryString`; then each method with a `null` context | `IsValidationEnabled` `true` before and after `EnableDynamicValidation`, which returned normally. The form getter answered `x` = `<script>`, the query getter `q` = `<script>`; the form getter returned the same instance on each call, and that instance was `Request.Unvalidated.Form`. `Request.Form["x"]` and `Request.QueryString["q"]` threw `HttpRequestValidationException`; `Request.Form["safe"]` answered `plain`. With a `null` context all three methods threw `NullReferenceException` |
| MWI4 | `HttpContextHelper.ExecuteInNullContext` with an action that captures `HttpContext.Current`, one that throws `InvalidOperationException`, a nested call, and `null` | Inside the action `HttpContext.Current` was `null`; afterwards it was the original instance again in every case. The thrown `InvalidOperationException` reached the caller unchanged. The nested call also saw `null`. A `null` action threw `NullReferenceException`, and the context was restored |
| MWI5 | `InfrastructureHelper.UnloadAppDomain()` from a request, then `HttpRuntime.UnloadAppDomain()` from a request, with `Application_End` recording `HostingEnvironment.ShutdownReason` | `UnloadAppDomainCalled` for both |
