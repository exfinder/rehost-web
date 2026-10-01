# Package reference

Add dependencies to the generated App project as `PackageReference` entries.
For each legacy `packages.config` entry, choose a replacement, remove a
redundant reference, or keep the original package.

## Replace these references

Packages bound to the .NET Framework's `System.Web` need Rehost replacements.
The replacements keep the package name with `Microsoft` changed to `Rehost`;
the table also covers the built-in `System.Web` assemblies.

| Legacy reference | Use |
| --- | --- |
| `System.Web`, `System.Web.Extensions`, `System.Web.ApplicationServices`, `System.Web.Services` | `Rehost.Web` |
| `Microsoft.AspNet.FriendlyUrls` | `Rehost.AspNet.FriendlyUrls` |
| `Microsoft.AspNet.Web.Optimization` | `Rehost.AspNet.Web.Optimization` |
| `Microsoft.AspNet.Web.Optimization.WebForms` | `Rehost.AspNet.Web.Optimization.WebForms` |
| `Microsoft.AspNet.ScriptManager.MSAjax` | `Rehost.AspNet.ScriptManager.MSAjax` |
| `Microsoft.AspNet.ScriptManager.WebForms` | `Rehost.AspNet.ScriptManager.WebForms` |
| `Microsoft.Owin.Host.SystemWeb` | `Rehost.Owin.Host.SystemWeb` |
| `Microsoft.AspNet.WebApi.WebHost` | `Rehost.AspNet.WebApi.WebHost` |
| `Microsoft.AspNet.WebPages` | `Rehost.AspNet.WebPages` |

`Rehost.Web` includes the assemblies `Rehost.Web`, `Rehost.Web.Extensions`,
`Rehost.Web.ApplicationServices`, and `Rehost.Web.Services` in place of the
four built-in references above.

## Remove redundant references

These packages are already dependencies of a Rehost package, conflict with it,
or need a different setup:

| Legacy package | Action |
| --- | --- |
| `Microsoft.AspNet.FriendlyUrls.Core` | Remove; included through `Rehost.AspNet.FriendlyUrls`. |
| `WebGrease`, `Antlr` | Remove; included through `Rehost.AspNet.Web.Optimization`. |
| `Microsoft.Owin`, `Owin` | Remove; included through `Rehost.Owin.Host.SystemWeb`. |
| `Microsoft.AspNet.WebApi.Core`, `Microsoft.AspNet.WebApi.Client` | Remove; included through `Rehost.AspNet.WebApi.WebHost`. |
| `Microsoft.AspNet.Razor` | Remove; included through `Rehost.AspNet.WebPages`. |
| `Microsoft.AspNet.WebApi` | Remove; it brings the original `Microsoft.AspNet.WebApi.WebHost` back in. |
| `Microsoft.Web.Infrastructure` | Remove; `Rehost.Web` already includes its API. Keeping both causes CS0433 when that API is used. |
| `AspNet.ScriptManager.jQuery`, `AspNet.ScriptManager.bootstrap` | Remove and register their script names in `PreApplicationStartCode.cs` in the App project. |
| `Microsoft.AspNet.Providers.Core` | Remove only if its sole use is an inactive `<sessionState customProvider>` entry while session mode is `InProc`. |

For packages supplied as dependencies, an explicit reference can stay if its
version meets the Rehost package's requirement. Prefer removing an unnecessary
reference to avoid carrying an older version.

## Keep other managed packages

Examples used by the application ports include:

- Entity Framework 6.3+, ASP.NET Identity 2.2, Newtonsoft.Json, Autofac, and log4net.
- Other Katana `Microsoft.Owin.*` packages.
- Other `Microsoft.AspNet.WebApi.*` packages, including `Cors`, `Tracing`, and `Owin`.

These keep their original names; some applications need newer versions than
their legacy project used. Check the code paths your application actually calls.
Compatibility in one application does not prove compatibility in another.

## Version conflicts and warnings

An explicit dependency version older than the required version fails restore
with `NU1605`. For example, `Rehost.AspNet.WebPages` requires
`Microsoft.AspNet.Razor` 3.3.0; an explicit 3.2.3 reference conflicts with it.

The template suppresses `NU1701` for packages that ship only a .NET Framework
build, such as `net45`. Suppressing the warning does not prove runtime
compatibility.

## Worked examples

- [Wingtip Toys](../apps/WingtipToys/DEVELOPMENT.md): package-by-package migration decisions.
- [Web Forms Identity template](../apps/WebFormsIdentityApplication/DEVELOPMENT.md): a smaller dependency list.

Return to the [migration checklist](migration.md).
