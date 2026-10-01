# Build and publish

These commands use the `Shop.Web` projects created in
[Getting started](getting-started.md). Substitute your application's name.

## Build

```text
dotnet build Shop.Web.Rehost.slnx
```

Always name the project or `.slnx` file. The folder also contains the legacy
`.sln`, so a bare `dotnet build` fails with `MSB1011`.

The host serves a copy of your application under `Shop.Web.Host/rehost_root/`.
Rebuild after editing an `.aspx` file so the host receives the updated copy.

A build copies an `App_Data` file only when it is absent from
`rehost_root/App_Data`. Application data therefore survives rebuilds.
Deleting `rehost_root/` also deletes that data.

## Change the development URL

The default URL is in `Shop.Web.Host/Properties/launchSettings.json`.
To choose another URL:

```text
dotnet run --project Shop.Web.Host -- --urls http://127.0.0.1:5090
```

## Publish

```text
dotnet publish Shop.Web.Host -c Release -o site
```

`site/` contains the website; `site/bin/` contains the binaries. Start it with:

```text
dotnet site/bin/Shop.Web.Host.dll --urls http://0.0.0.0:8080
```

`launchSettings.json` is used for development and is not published, so supply
the URL when starting the published host.

## Configuration changes

During a build, `Web.Rehost.config` applies changes to the original
`Web.config` and writes the result to `Shop.Web.Host/rehost_root/web.config`.
The original file stays unchanged.

For a Release publish, `Web.Release.config` is applied first and
`Web.Rehost.config` second. Restart the process after changing configuration;
automatic file-change reload is not implemented.

See the [migration guide](migration.md#configuration) for configuration examples.

## Generated files

| File | Purpose |
| --- | --- |
| `Shop.Web.App.csproj` | Builds application code from the folder named by `RehostAppContentRoot`. `RehostAppContentExcludes` leaves files out. |
| `Shop.Web.Host.csproj` | `RehostSiteContentRoot` names the application folder to copy. `OutDir` sends the binaries to `rehost_root/bin/`. |
| `Program.cs` | Starts the ASP.NET Core host using `AddRehostWeb` and `UseRehostWeb`. |
| `Web.Rehost.config` | Applies configuration changes to the application's copy. |
| `Properties/launchSettings.json` | Sets the development URL. |
| `.gitignore` | Keeps `rehost_root/` build output out of source control. |
