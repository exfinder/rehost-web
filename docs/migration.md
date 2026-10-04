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
| MVC 5 | `Rehost.AspNet.Mvc` |

The template already includes the common Web Forms packages. Keep only those
your application uses. See the [full package reference](package-reference.md)
for exact legacy names, redundant references, and version conflicts.

## Configuration

Edit the generated Host project's `Web.Rehost.config`, which applies changes
to a copy of your `Web.config`. Keep the three supplied adjustments and add
your own below them.

Check these settings:

- **Database connections:** replace LocalDB or `AttachDbFilename` with a
  connection your environment can use. Write `|DataDirectory|/file`, not
  `|DataDirectory|\file`: the database driver joins that path, and off Windows
  a backslash becomes part of the file name.
- **Modules and handlers:** remove registrations for libraries that cannot
  run on modern .NET, including any related `<location>` blocks.
- **Compilation assemblies:** remove `<compilation><assemblies>` entries for
  assemblies .NET does not ship, such as `System.Management` or
  `System.Net.Http.WebRequest`:

  ```xml
  <system.web>
    <compilation>
      <assemblies>
        <add xdt:Locator="Condition(starts-with(@assembly,'System.Management,'))" xdt:Transform="Remove" />
      </assemblies>
    </compilation>
  </system.web>
  ```

- **Framework assembly names:** leave them as written. The build rewrites
  `System.Web`, Web Pages, MVC, Optimization and similar assembly names in
  every staged `web.config` to their Rehost names. See the
  [table](dev/migration-reference.md#configuration).

Carry over any application settings previously supplied by the server's
`machine.config`, root `web.config`, or `applicationHost.config`.

Examples: [Wingtip Toys](../apps/WingtipToys/DEVELOPMENT.md#webconfig),
[eShopLegacyWebForms](../apps/eShopLegacyWebForms/DEVELOPMENT.md#webconfig), and
[YAF](../apps/YAF/DEVELOPMENT.md#webconfig).

## Preserved source

The generated App project compiles every C# file in the application folder:

```xml
<Compile Include="$(RehostAppContentRoot)**/*.cs" />
<Compile Remove="$(RehostAppContentRoot)bin/**" />
<Compile Remove="$(RehostAppContentRoot)obj/**" />
<Compile Remove="$(RehostAppContentRoot)packages/**" />
```

If it picks up files your old project excluded, add a `<Compile Remove>` line
for each one below these.

Special folders such as `App_Code` are compiled by the runtime. See
[source inclusion](dev/migration-reference.md#preserved-source) if you encounter
duplicate types or are moving a Web Site project.

Check your old custom build steps too: generated code or assets may not exist
until those steps run. The
[build-step reference](dev/migration-reference.md#custom-build-steps) covers examples.

## Class libraries that use System.Web

Give each class library in your solution that uses `System.Web`, such as a
`BlogEngine.Core` beside the web project, its own SDK project in a new folder
beside the App project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>MyLibrary</AssemblyName>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <RehostAppContentRoot>../MyLibrary/</RehostAppContentRoot>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Rehost.Web" Version="…" />
  </ItemGroup>
</Project>
```

- Point `RehostAppContentRoot` at the library's legacy folder and add the
  `Compile` lines from [Preserved source](#preserved-source).
- Keep the legacy `AssemblyName`: `web.config` names the library's types by it.
- Reference `Rehost.Web` at the App project's version, not the hosting
  package. Add other Rehost packages only when the library itself uses them.
- Add a `ProjectReference` to the new project from the App project.

Example: [BlogEngine.NET](../apps/BlogEngine/DEVELOPMENT.md#layout) builds its
`BlogEngine.Core` library this way.

## Libraries bound to System.Web

A library referencing the .NET Framework's `System.Web` needs a replacement
or recompilation against Rehost.Web; binding redirects cannot fix it.

Try the application's actual code paths before deciding to rebuild a library.
See [library migration](dev/migration-reference.md#libraries-bound-to-systemweb)
for examples. Older C# code that fails under the newer compiler may need a
[language-version setting](dev/migration-reference.md#language-pins).

## Machine keys

For one instance, default keys persist locally and survive process restarts.
In containers, preserve the key file between container replacements to keep
cookies valid.

For multiple instances, supply shared keys through both
`REHOST_MACHINEKEY_*` variables or explicit `<machineKey>` values. Do not mix
environment overrides with explicit keys. See
[key configuration](dev/migration-reference.md#machine-keys) for exact settings
and limits when sharing keys with .NET Framework.

## Check your application

Run the flows your users rely on: sign-in, postbacks, uploads, and any stateful
operations. The [example applications](../apps/README.md) show concrete journeys.

- For startup failures, use [Troubleshooting](troubleshooting.md).
- For publishing, use [Build and publish](build-and-publish.md).
- For containers or custom file locations, check the
  [runtime storage settings](dev/migration-reference.md#generated-output-codegen).
