# Rehost.Web

System.Web on modern .NET. Run predominantly managed ASP.NET Web Forms, Web
Pages and Web API applications across Windows, Linux, and macOS with minimal
application-source changes.

The `Rehost.Web` package is the starting point: it carries the
`System.Web`-compatible runtime and its companion assemblies, among them the
ones the root configuration names, so `asp:` tags parse out of the box.
Packages that were NuGet packages on .NET Framework map to `Rehost.*`
counterparts, so your `packages.config` translates line by line; the
[package mapping](https://github.com/exfinder/rehost-web/blob/main/docs/migration.md#package-mapping)
lists each one. A host executable references
`Rehost.Web.AspNetCore` and serves the site through the classic managed
pipeline on Kestrel.

The legacy application folder is never modified and stays buildable on
.NET Framework side by side.

## Getting started

In the folder that contains the legacy web application folder:

```text
dotnet new install Rehost.Web.Templates
dotnet new rehost-web --webapp MyApp
dotnet run --project MyApp.Host
```

The template adds `MyApp.App` and `MyApp.Host` beside `MyApp/`. The full walk-through
is [docs/getting-started.md](https://github.com/exfinder/rehost-web/blob/main/docs/getting-started.md).

See the repository for documentation, compatibility claims, and samples.
