# Getting started

Run an existing C# ASP.NET Web Application Project on .NET 10. Your original
project stays unchanged and can still build on .NET Framework.

To try Rehost.Web before migrating your app, run the
[Web Forms example](../apps/WebFormsApplication/README.md).

## Before you start

- Install the .NET 10 SDK (10.0.302 or later).
- Have your web application folder ready, including its `.csproj` and
  `Web.config`. Web Site projects (no `.csproj`, code in `App_Code`) need
  separate setup.
- Check [compatibility](what-works.md) for your app's features and dependencies.

Rehost.Web needs no IIS or Visual Studio. A database or Docker is needed only
if your application requires it.

## 1. Install the template

```text
dotnet new install Rehost.Web.Templates
```

## 2. Add projects to build and host your app

Go to the folder containing your web application, usually beside the old `.sln`.
Replace `MyApp` with your application's folder name:

```text
cd path/to/solution
dotnet new rehost-web --webapp MyApp
```

The result:

```text
MyApp/                  legacy application, untouched
MyApp.App/              compiles the legacy *.cs into MyApp.dll
MyApp.Host/             the process that replaces IIS
MyApp.Rehost.slnx
```

The folder name also becomes the application assembly name. If these differ,
[specify the original assembly name](troubleshooting.md#application-assembly-name).

## 3. Add your dependencies

Open `MyApp.App/MyApp.App.csproj`. Use the
[package mapping](package-reference.md) to carry over dependencies from
your old `packages.config`, replacing packages tied to `System.Web`.

The template includes Friendly URLs, Optimization, and ScriptManager packages.
Remove those your app does not use; the `Antlr` and `WebGrease` lines go with
Optimization. Packages such as Entity Framework 6, Autofac, log4net, and
Newtonsoft.Json keep their original names.

## 4. Adjust configuration

Open `MyApp.Host/Web.Rehost.config`. This file applies changes to a copy of
your `Web.config`; it uses the same XDT format as `Web.Release.config`.

Keep the supplied adjustments. Add changes your app needs, such as
connection strings or removal of modules that cannot run on .NET 10.
See [configuration examples](migration.md#configuration).

## 5. Run

```text
dotnet run --project MyApp.Host
```

Open the URL printed in the terminal. For the stock Visual Studio Web Forms
template, the home page should render and postbacks should work.

After editing pages or configuration, stop the host with **Ctrl+C** and run
the same command again. This rebuilds the site and starts a new process.

## Next steps

- [Troubleshooting](troubleshooting.md): build and startup problems.
- [Build and publish](build-and-publish.md): output files, rebuilds, and publishing.
- [Migration guide](migration.md): machine keys, libraries, and application-specific changes.
