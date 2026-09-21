# Rehost WebForms sample application

A browsable Web Forms application on the rehosted runtime: every page is a real
`.aspx` compiled at first request by the `System.Web` pipeline, served over
Kestrel.

```text
dotnet run --project samples/Rehost.WebForms.SampleApp
```

Then open <http://127.0.0.1:5080/Default.aspx>. Add `-- --urls <url>` to move
the endpoint.

`dotnet publish samples/Rehost.WebForms.SampleApp -o X` gives the same shape:
`X/` is the site root and the binaries go to `X/bin/`. Start it with
`dotnet X/bin/Rehost.WebForms.SampleApp.dll --urls http://127.0.0.1:5080`;
`launchSettings.json` is a development file and is not published.

The site lives in `rehost_root/`, laid out like a classic Web Site: pages,
`App_Code`, `App_GlobalResources`, and `web.config` at its root, binaries under
`rehost_root/bin/`. `Program.cs`, the project file and `obj/` stay outside it,
so the runtime cannot serve them. Pages compile from source at first request, so
editing an `.aspx` needs only a restart, not a rebuild. The layout is a demo convenience — the primary
compatibility target is the Web Application Project model that real
enterprise applications use, which `WapDemo.aspx` demonstrates:
`CodeBehind` + `Inherits` + designer partial compiled by MSBuild into the
`bin` assembly (see
[web-site-vs-wap-project-models](../../docs/follow-ups/web-site-vs-wap-project-models.md)).

It demonstrates the currently supported surface: `Site.master` chrome,
`App_Code` (including a control-state control), `App_GlobalResources` with a
culture satellite, postback/view state, the WAP-style page above, user
controls with an `OutputCache` fragment, cookies,
multipart upload with `SaveAs`, `Response.End` and friends, request
validation, and static files through `StaticFileHandler`.

