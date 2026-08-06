# Rehost WebForms sample application

A browsable Web Forms application on the rehosted runtime: every page is a real
`.aspx` compiled at first request by the `System.Web` pipeline, served over
Kestrel.

```text
dotnet run --project samples/Rehost.WebForms.SampleApp
```

Then open <http://127.0.0.1:5080/Default.aspx>. Pass a different URL as the
first argument to move the endpoint.

The project lays out like a classic Web Site: pages, `App_Code`,
`App_GlobalResources`, and `web.config` at the root, binaries under `bin/`.
Pages compile from source at first request, so editing an `.aspx` needs only a
restart, not a rebuild. It demonstrates the currently supported surface: `Site.master` chrome, `App_Code` (including a
control-state control), `App_GlobalResources` with a culture satellite,
postback/view state, cookies, multipart upload with `SaveAs`, `Response.End`
and friends, request validation, and static files through `StaticFileHandler`.

Known limitation worth knowing while editing pages: code compiled at runtime
cannot name `HttpUtility` (`CS0433` against the shared framework's
`System.Web.HttpUtility.dll`) — use `Server.HtmlEncode` or
`System.Net.WebUtility` until the reference-set issue in
[`runtime-codegen-and-loading`](../../docs/follow-ups/runtime-codegen-and-loading.md)
is resolved.
