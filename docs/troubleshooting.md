# Troubleshooting

Use these checks when following [Getting started](getting-started.md).

## Missing package types

If a type from `Microsoft.AspNet.*` or `Microsoft.Owin.Host.SystemWeb` is
missing, add its Rehost counterpart from the
[package mapping](migration.md#package-mapping).

An original package bound to the .NET Framework's `System.Web` may compile
but fail at runtime. Use a replacement package or
[rebuild the library](migration.md#libraries-bound-to-systemweb).

The template suppresses `NU1701`, the restore warning for packages that only
ship a .NET Framework build. This does not prove a package's runtime
compatibility.

## CS0246 in an unused source file

The App project includes C# files from the application folder, including files
your legacy project may have left out. List unused files in
`RehostAppContentExcludes`; see
[source inclusion](migration.md#preserved-source).

## Application assembly name

The template uses the application's folder name as its assembly name.
`Web.config` may refer to that assembly in a setting such as
`type="…, Shop.Web"`.

If the original assembly is `Shop.Web` but its folder is `Web`, supply both:

```text
dotnet new rehost-web --webapp Web --assembly Shop.Web
```

The template uses the supplied assembly name for both new projects and the
application assembly.

## MSB1011 when building

Name the project or solution explicitly. The old `.sln` and new `.slnx` are in
the same folder. For example:

```text
dotnet build Shop.Web.Rehost.slnx
```

## Windows-only dependencies

APIs requiring the registry, COM, or DPAPI cannot run in the portable runtime.
Check [compatibility](what-works.md#what-is-unavailable-or-untested) and
replace the dependency or change the application path that uses it.

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

The [application notes](../apps/WebFormsApplication/README.md) describe the
sample's setup and checks.
