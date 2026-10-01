# Rehost.Web

**Run ASP.NET Web Forms on Linux, macOS, and Windows.**

Rehost.Web brings classic ASP.NET applications to .NET 10 with minimal
application changes. Keep your `.aspx` pages, code-behind, and `Web.config`.
Also supports Web Pages and Web API.

It ports the .NET Framework's `System.Web` runtime from Microsoft Reference
Source and uses Kestrel to host your application in place of IIS.

**Alpha.** Tested with real applications.
See [compatibility](docs/what-works.md) and the [roadmap](ROADMAP.md).

## Quick start

Requires the [.NET 10 SDK](docs/getting-started.md). From the folder containing
your existing web application folder, replace `MyApp` with that folder's name:

```text
dotnet new install Rehost.Web.Templates
dotnet new rehost-web --webapp MyApp
dotnet run --project MyApp.Host
```

The template adds projects to build and host your app, leaving the original
project untouched. Before running, carry over your dependencies and adjust
configuration as described in [Getting started](docs/getting-started.md).

## Running examples

- [Web Forms template](apps/WebFormsApplication/README.md): the familiar
  Visual Studio app, with working postbacks, routing, scripts, and styles.
- [YAF forum](apps/YAF/README.md): registration, sign-in, topics, replies,
  moderation, and search, backed by PostgreSQL. Rebuilt with compatibility
  changes.

More [example applications](apps/README.md), including Wingtip Toys,
eShopLegacyWebForms, and the AJAX Control Toolkit sample site.

## Limitations

- Features tied to Windows or IIS are unsupported.
- Libraries bound to the .NET Framework's `System.Web` need recompilation or a
  [replacement package](docs/package-reference.md).
- Visual Basic pages are unsupported. The quickstart covers Web Application
  Projects; Web Site projects need separate setup.

Check [what works](docs/what-works.md) for your app's features.

## Documentation

[Getting started](docs/getting-started.md) ·
[Migration guide](docs/migration.md) ·
[Documentation](docs/README.md) ·
[Contributing](CONTRIBUTING.md)

[MIT license](LICENSE). Imported code retains its original licenses;
see [third-party notices](THIRD-PARTY-NOTICES.txt).
[Security policy](SECURITY.md).
