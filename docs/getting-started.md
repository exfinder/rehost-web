# Getting started

Run an existing C# ASP.NET Web Application Project on .NET 10. Your original
project stays unchanged and can still build on .NET Framework.

## Before you start

- Install .NET SDK 10.0.302 or a later 10.0.3xx release. Validation used runtime
  10.0.10 on Windows x64, Linux x64 and arm64, and macOS arm64.
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
Replace `Shop.Web` with your application's folder name:

```text
cd path/to/solution
dotnet new rehost-web --webapp Shop.Web
```

The result:

```text
Shop.Web/                  legacy application, untouched
Shop.Web.App/              compiles the legacy *.cs into Shop.Web.dll
Shop.Web.Host/             the process that replaces IIS
Shop.Web.Rehost.slnx
```

The folder name also becomes the application assembly name. If these differ,
[specify the original assembly name](troubleshooting.md#application-assembly-name).

## 3. Add your dependencies

Open `Shop.Web.App/Shop.Web.App.csproj`. Use the
[package mapping](package-reference.md) to carry over dependencies from
your old `packages.config`, replacing packages tied to `System.Web`.

The template includes Friendly URLs, Optimization, and ScriptManager packages.
Remove those your app does not use. Packages such as Entity Framework 6,
Autofac, log4net, and Newtonsoft.Json keep their original names.

## 4. Adjust configuration

Open `Shop.Web.Host/Web.Rehost.config`. This file applies changes to a copy of
your `Web.config`; it uses the same XDT format as `Web.Release.config`.

Keep the three supplied adjustments. Add changes your app needs, such as
connection strings or removal of modules that cannot run on .NET 10.
See [configuration examples](migration.md#configuration).

## 5. Run

```text
dotnet run --project Shop.Web.Host
```

Open the URL printed in the terminal. For the stock Visual Studio Web Forms
template, the home page should render and postbacks should work.

After editing pages, rebuild and restart to serve the updated copy.

## Next steps

- [Troubleshooting](troubleshooting.md): build and startup problems.
- [Build and publish](build-and-publish.md): output files, rebuilds, and publishing.
- [Migration guide](migration.md): machine keys, libraries, and application-specific changes.
