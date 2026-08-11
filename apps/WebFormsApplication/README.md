# WebFormsApplication

Frozen .NET Framework 4.8.1 Visual Studio Web Forms template application.
The legacy `packages.config` installs copied the tracked `Content/` and
`Scripts/` files into the application. Package restore recreates `packages/`,
not missing application content. Keep these assets committed; review package
upgrade additions and deletions as ordinary source changes.

Run `dotnet msbuild apps/WebFormsApplication/build.proj` from the repository
root. The driver incrementally produces ignored local packages, then builds the
SDK-style WAP sidecar through package references only and stages the runnable
site under `artifacts/webforms-application/site`.

Then run:

```text
dotnet artifacts/webforms-application/site/bin/WebFormsApplication.Host.dll
```

Open <http://127.0.0.1:5081/Default>. Pass another URL as the first argument to
move the endpoint.
