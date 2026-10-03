# Troubleshooting

Use these checks when following [Getting started](getting-started.md) or
[migrating an application](migration.md).

## Missing package types

If a type from `Microsoft.AspNet.*` or `Microsoft.Owin.Host.SystemWeb` is
missing, add its Rehost counterpart from the
[package mapping](package-reference.md).

An original package bound to the .NET Framework's `System.Web` may compile
but fail at runtime. Use a replacement package or
[rebuild the library](migration.md#libraries-bound-to-systemweb).

`NU1701` is the restore warning for a package that ships only a .NET Framework
build. The template silences it only for `Antlr` and `WebGrease`, which
`Rehost.AspNet.Web.Optimization` brings in; drop those two lines if you drop
that package. Any other `NU1701` means the package can fail at runtime: prefer a
version with a .NET or .NET Standard build, or test the code paths you call.
See [warnings](package-reference.md#version-conflicts-and-warnings).

## CS0246 in an unused source file

The App project compiles every C# file in the application folder, including
files your legacy project may have left out. Add a `<Compile Remove>` line for
each unused file to the App project; see
[source inclusion](migration.md#preserved-source).

## Application assembly name

The template uses the application's folder name as its assembly name.
`Web.config` may refer to that assembly in a setting such as
`type="…, MyApp"`.

If the original assembly is `MyApp` but its folder is `Web`, supply both:

```text
dotnet new rehost-web --webapp Web --assembly MyApp
```

The template uses the supplied assembly name for both new projects and the
application assembly.

## MSB1011 when building

Name the project or solution explicitly. The old `.sln` and new `.slnx` are in
the same folder. For example:

```text
dotnet build MyApp.Rehost.slnx
```

## Windows-only dependencies

APIs requiring the registry, COM, or DPAPI cannot run in the portable runtime.
Check [compatibility](what-works.md#what-is-unavailable-or-untested) and
replace the dependency or change the application path that uses it.

## CS0433 from duplicate types

Check whether a folder compiled at runtime was also compiled into your App
assembly. `App_Code` is a common cause. See
[source inclusion](dev/migration-reference.md#preserved-source).

Also remove an explicit `Microsoft.Web.Infrastructure` package reference:
`Rehost.Web` already includes the same API. The
[package reference](package-reference.md#remove-redundant-references) lists
other redundant packages.

## Configuration errors

A refused configuration setting fails activation with the file and entry in
the message. A `web.config` that cannot be parsed returns ASP.NET's
Configuration Error page on every request, then ends the process with exit
code 82.

Fix the setting through `Web.Rehost.config`. Editing `rehost_root/web.config`
directly loses the change on the next build. See
[configuration changes](migration.md#configuration) and the
[detailed compatibility reference](dev/compatibility.md#configuration-and-iis-derived-behavior)
for individual settings.

## Application_Start fails

The exception is logged once. Every request receives HTTP 500 for ten seconds,
then the process exits for replacement. With a supervisor configured to
replace it, `Application_Start` runs again, matching integrated IIS behavior.

Fix the startup exception before retrying. Rehost.Web uses process replacement
for restarts; it does not restart an AppDomain inside the same process.

## An assembly is missing on the first request

A `FileNotFoundException` naming `System.Web`, `System.Net.Http.WebRequest`,
or a `System.Configuration` facade can come from a library built for .NET
Framework. Check the library's runtime dependencies and
[rebuild it if necessary](dev/migration-reference.md#libraries-bound-to-systemweb).

## A page exists but returns 404

Check the handlers and rewrite rules serving that URL. Your application may
depend on a rule or handler the port does not honor. See the
[configuration reference](dev/compatibility.md#configuration-and-iis-derived-behavior).

## Find the application's files

The startup log prints `Rehost physical root path` for the staged site and
`Rehost compilation temp path` for generated assemblies.
`AppContext.BaseDirectory` becomes the site root from the first Web Forms
request; before then, it points at the host binaries. See
[base-directory behavior](dev/migration-reference.md#the-base-directory).

## Check the stock Web Forms template

For the Visual Studio template, opening `/` should return HTTP 200 and render
the home page. Its form posts to `./`. A POST carrying the rendered
`__VIEWSTATE`, `__VIEWSTATEGENERATOR`, and `__EVENTVALIDATION` fields should
return HTTP 200 and render the same page again. Friendly URLs redirects
`/Default.aspx` to `/Default` with HTTP 301.

From this repository, check that journey and the script bundles with:

```text
apps/WebFormsApplication/smoke.sh <url>
```

The [application notes](../apps/WebFormsApplication/DEVELOPMENT.md) describe the
sample's setup and checks.
