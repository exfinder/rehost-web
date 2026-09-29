# Rehost.Web

System.Web on modern .NET. Rehost.Web runs ASP.NET Web Forms, Web Pages and
Web API applications on .NET 10, on Windows, Linux and macOS, with the legacy
project untouched. The runtime is a port of `System.Web` from
Microsoft's Reference Source; applications keep their markup, code-behind and
`Web.config`, and gain a Kestrel host, `dotnet build` and `dotnet publish`.

**Alpha.** Validated on Windows x64, Linux x64, Linux arm64 and macOS arm64
with SDK 10.0.302 and runtime 10.0.10. Production deployment is not part of
this release; see the [roadmap](ROADMAP.md).

## Quick start

In the folder that contains the legacy web application folder:

```text
dotnet new install Rehost.Web.Templates
dotnet new rehost-web --webapp MyApp
dotnet run --project MyApp.Host
```

The template adds `MyApp.App` (compiles the legacy `*.cs`) and `MyApp.Host`
(the process that replaces IIS) beside `MyApp/`. The full walk-through with
prerequisites, packages, configuration and publish is
[Getting started](docs/getting-started.md); the migration steps that follow it
are in [Migrating an application](docs/migration.md).

## Packages

Eleven packages, one version. `Rehost.Web` is the runtime and takes the place
of the in-box assemblies, `Rehost.Web.AspNetCore` is the Kestrel host, and
`Rehost.Web.Templates` carries `dotnet new rehost-web`. Every other package
replaces the NuGet package of the same name with `Microsoft` changed to
`Rehost`, so `packages.config` translates line by line; the
[package mapping](docs/migration.md#package-mapping) lists each legacy
reference and its counterpart.

## Examples

Each application under `apps/` is a frozen .NET Framework tree plus an
App/Host pair, built from the packages the way a consumer builds. Each has a
`smoke.sh` that walks its journey.

| Application | What it proves |
| --- | --- |
| [WebFormsApplication](apps/WebFormsApplication/README.md) | The Visual Studio template: default document, postback, Friendly URLs, bundles, mobile master. No database. |
| [WebFormsIdentityApplication](apps/WebFormsIdentityApplication/README.md) | The "Individual User Accounts" template: OWIN, ASP.NET Identity 2.2, Entity Framework 6, SQLite. |
| [eShopLegacyWebForms](apps/eShopLegacyWebForms/README.md) | Microsoft's catalog manager: Autofac, EF6 on mock data, log4net, `MapPageRoute`. |
| [WingtipToys](apps/WingtipToys/README.md) | The tutorial store: register, sign in, session cart, role-gated admin, checkout, against SQL Server. |
| [AjaxControlToolkitSampleSite](apps/AjaxControlToolkitSampleSite/README.md) | A Web Site project and a large third-party control library, recompiled. |
| [YAF](apps/YAF/README.md) | YAF.NET 3.2.16: fourteen source projects, Identity over OWIN, URL rewriting, Web API, Lucene search, on PostgreSQL. Its substitutions and untested areas are listed in its README and [provenance](docs/provenance/yafnet.md). |

## What is out of contract

[Compatibility](docs/compatibility.md) is the support map; the short version:

- anything that needs Windows, IIS, the registry, COM, DPAPI or
  WindowsDesktop is not part of the portable runtime and fails with an
  explicit message where reached;
- binary compatibility with Microsoft's strong-named `System.Web`: a library
  bound to it is recompiled against this runtime, not redirected;
- Visual Basic pages, XSD typed data sets and `.wsdl` build providers;
- partial trust and CAS policy; Dynamic Data, Entity and Mobile stay
  unassessed, as the map states per row.

## Read next

- [Getting started](docs/getting-started.md) and
  [Migrating an application](docs/migration.md)
- [Compatibility and evidence](docs/compatibility.md)
- [Roadmap](ROADMAP.md) and [documentation index](docs/README.md)
- [Contributing](CONTRIBUTING.md), [Security](SECURITY.md),
  [License](LICENSE) (MIT; imported trees keep their own, see
  [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt))
