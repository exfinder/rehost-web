# Rehost WebForms

Run predominantly managed ASP.NET Web Forms applications on modern .NET across
Windows, Linux, and macOS with minimal application-source changes.

The `Rehost.WebForms` package is the starting point: it carries the
`System.Web`-compatible runtime and its companion assemblies, among them the
ones the root configuration names, so `asp:` tags parse out of the box.
Packages that were NuGet packages on .NET Framework (`FriendlyUrls`, `Optimization`,
`ScriptManager`) map one-to-one to their `Rehost.*` counterparts — translate
your `packages.config` line by line. A host executable references
`Rehost.WebForms.Hosting` and serves the site through the classic managed
pipeline on Kestrel.

The legacy application folder is never modified and stays buildable on
.NET Framework side by side.

## Getting started

In the folder that contains the legacy web application folder:

```text
dotnet new install Rehost.WebForms.Templates
dotnet new rehost-webforms --webapp MyApp
dotnet run --project MyApp.Host
```

The template adds `MyApp.App` and `MyApp.Host` beside `MyApp/`. The full walk-through
is [docs/getting-started.md](https://github.com/exfinder/rehost-webforms/blob/main/docs/getting-started.md).

See the repository for documentation, compatibility claims, and samples.
