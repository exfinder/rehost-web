# Migrating an application

Use this checklist alongside [Getting started](getting-started.md) to adapt
an existing application. Check [compatibility](what-works.md) first.

Make build and configuration changes in the generated App and Host projects.
Your original .NET Framework project stays unchanged.

## Package mapping

Open the generated App `.csproj`. For each `packages.config` entry:

- Replace packages bound to the .NET Framework's `System.Web`.
- Remove packages already supplied by a Rehost package.
- Keep other managed packages, checking their runtime dependencies.

Common replacements:

| Your application uses | Add |
| --- | --- |
| `System.Web` and companion assemblies | `Rehost.Web` |
| Friendly URLs | `Rehost.AspNet.FriendlyUrls` |
| Bundling and minification | `Rehost.AspNet.Web.Optimization` |
| Web Forms bundle controls | `Rehost.AspNet.Web.Optimization.WebForms` |
| Microsoft AJAX scripts | `Rehost.AspNet.ScriptManager.MSAjax` |
| Web Forms scripts | `Rehost.AspNet.ScriptManager.WebForms` |
| OWIN hosting | `Rehost.Owin.Host.SystemWeb` |
| Web API 2 hosting | `Rehost.AspNet.WebApi.WebHost` |
| Web Pages | `Rehost.AspNet.WebPages` |

The template already includes the common Web Forms packages. Keep only those
your application uses. See the [full package reference](package-reference.md)
for exact legacy names, redundant references, and version conflicts.

## Configuration

Edit `<Host>/Web.Rehost.config`, which applies changes to a copy of your
`Web.config`. Keep the three supplied adjustments and add your own below them.

Check these settings:

- **Database connections:** replace LocalDB or `AttachDbFilename` with a
  connection your environment can use.
- **Modules and handlers:** remove registrations for libraries that cannot
  run on modern .NET, including any related `<location>` blocks.
- **Web Pages assemblies:** use the Rehost assembly names in Razor sections,
  build providers, and assembly registrations. See the
  [exact names](migration-reference.md#configuration).

Carry over any application settings previously supplied by the server's
`machine.config`, root `web.config`, or `applicationHost.config`.

Examples: [Wingtip Toys](../apps/WingtipToys/DEVELOPMENT.md#webconfig),
[eShopLegacyWebForms](../apps/eShopLegacyWebForms/DEVELOPMENT.md), and
[YAF](../apps/YAF/DEVELOPMENT.md#webconfig).

## Preserved source

The generated App project includes C# files from the application folder.
If it picks up files your old project excluded, add them to
`RehostAppContentExcludes`.

Special folders such as `App_Code` are compiled by the runtime. See
[source inclusion](migration-reference.md#preserved-source) if you encounter
duplicate types or are moving a Web Site project.

Check your old custom build steps too: generated code or assets may not exist
until those steps run. The
[build-step reference](migration-reference.md#custom-build-steps) covers examples.

## Libraries bound to System.Web

A library referencing the .NET Framework's `System.Web` needs a replacement
or recompilation against Rehost.Web; binding redirects cannot fix it.

Try the application's actual code paths before deciding to rebuild a library.
See [library migration](migration-reference.md#libraries-bound-to-systemweb)
for examples. Older C# code that fails under the newer compiler may need a
[language-version setting](migration-reference.md#language-pins).

## Machine keys

For one instance, default keys persist locally and survive process restarts.
In containers, preserve the key file between container replacements to keep
cookies valid.

For multiple instances, supply shared keys through both
`REHOST_MACHINEKEY_*` variables or explicit `<machineKey>` values. Do not mix
environment overrides with explicit keys. See
[key configuration](migration-reference.md#machine-keys) for exact settings
and limits when sharing keys with .NET Framework.

## Check your application

Run the flows your users rely on: sign-in, postbacks, uploads, and any stateful
operations. The [example applications](../apps/) show concrete journeys.

- For startup failures, use [Troubleshooting](troubleshooting.md).
- For publishing, use [Build and publish](build-and-publish.md).
- For containers or custom file locations, check the
  [runtime storage settings](migration-reference.md#generated-output-codegen).
