# Will my app work?

Rehost.Web runs classic ASP.NET applications on .NET 10. Compatibility is
tested through real applications and individual features.
Your app's dependencies and configuration determine what needs changing.

## What can I run?

C# applications using Web Forms, Web Pages, MVC 5, or Web API 2. Validated platforms
are Windows x64, Linux x64 and arm64, and macOS arm64.

The [getting started guide](getting-started.md) covers Web Application Projects
(apps with a `.csproj`). Web Site projects also have running examples, but need
separate setup.

## What has been tested?

| Feature | What to expect |
| --- | --- |
| Web Forms | Pages, common controls, master pages, user controls, postbacks, and view state work. Coverage varies by control and feature. |
| AJAX | ScriptManager and UpdatePanel partial updates work. Some debug and localized script resources are unavailable. |
| Web Pages | `.cshtml` pages, layouts, and page includes work. `System.Web.Helpers` and WebMatrix data/authentication libraries are not included. |
| MVC 5 | Controllers, areas, and Razor views with layouts work through the Rehost MVC package. Libraries built against `System.Web.Mvc`, such as Autofac.Mvc5, need recompiling. |
| Web API 2 | Controllers, JSON requests and responses, and model binding work through the Rehost Web API host package. |
| Sign-in and roles | ASP.NET Identity 2.2 cookie sign-in and role checks are exercised. Provider-based authentication has remaining gaps. |
| Session | In-process session and custom providers work. Third-party providers need compatibility validation. SQL Server session mode is not yet implemented; StateServer is unsupported. Expiry events remain untested. |

See the [Web Forms template](../apps/WebFormsApplication/README.md),
[Wingtip Toys store](../apps/WingtipToys/README.md),
[YAF forum](../apps/YAF/README.md), and
[eShop MVC catalog](../apps/eShopLegacyMVC/README.md) for running examples and their setup.

## What might need changing?

- Replace packages tied to the .NET Framework's `System.Web` with their
  [Rehost counterparts](package-reference.md), or rebuild the libraries.
- Adjust connection strings and configuration through the generated
  `Web.Rehost.config`. Your original `Web.config` stays untouched.
- Replace Windows-specific dependencies. For example, LocalDB needs a different
  database connection.
- Rebuild and restart after editing pages or configuration.
  Automatic file-change reload is not implemented.
- Fix backslash path literals in application code; on Linux and macOS a
  backslash is part of the file name.

The [migration guide](migration.md) explains these steps.

## What is unavailable or untested?

Windows authentication, impersonation, and features requiring IIS native
components, COM, the registry, DPAPI, or WindowsDesktop are unsupported.
Visual Basic pages, XSD typed dataset generation, `.wsdl` build providers,
and partial trust are also unsupported.

Dynamic Data, `EntityDataSource`, and legacy Mobile controls remain unassessed.
A feature missing from this overview may be supported, limited, or untested.

For a specific API or configuration setting, consult the
[detailed compatibility reference](dev/compatibility.md). It contains the exact
boundaries and test evidence behind this overview.
