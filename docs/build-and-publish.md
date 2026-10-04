# Build and publish

These commands use the `MyApp` projects created in
[Getting started](getting-started.md). Substitute your application's name.

## Build

```text
dotnet build MyApp.Rehost.slnx
```

Always name the project or `.slnx` file. The folder also contains the legacy
`.sln`, so a bare `dotnet build` fails with `MSB1011`.

The host serves a copy of your application under `MyApp.Host/rehost_root/`.
After editing pages or configuration, stop the host and run
`dotnet run --project MyApp.Host` again to rebuild and restart it.

A build copies an `App_Data` file only when it is absent from
`rehost_root/App_Data`. Application data therefore survives rebuilds.
Deleting `rehost_root/` also deletes that data.

## Change the development URL

The default URL is in `MyApp.Host/Properties/launchSettings.json`.
To choose another URL:

```text
dotnet run --project MyApp.Host -- --urls http://127.0.0.1:5090
```

## Publish

```text
dotnet publish MyApp.Host -c Release -o site
```

`site/` contains the website; `site/bin/` contains the binaries. Start it with:

```text
dotnet site/bin/MyApp.Host.dll --urls http://0.0.0.0:8080
```

`launchSettings.json` is used for development and is not published, so supply
the URL when starting the published host.

## Configuration changes

During a build, `Web.Rehost.config` applies changes to the original
`Web.config` and writes the result to `MyApp.Host/rehost_root/web.config`.
The original file stays unchanged. The build rewrites that staged file only
when `Web.config`, `Web.Rehost.config` or the set of transforms has changed
since the last build; any edit the application or you made to it is lost then.

An application that writes its own `web.config` at install time, for a
machine key, a connection string or installed extensions, must be installed
and operated from a publish output, as it was from a deployed site on IIS.
`rehost_root` is the development copy the build owns. A later publish into
the same folder overwrites `web.config` as a redeploy did on IIS; back it up
and merge as the application's own upgrade instructions say.

For a Release publish, `Web.Release.config` is applied first and
`Web.Rehost.config` second. Restart the process after changing configuration;
automatic file-change reload is not implemented.

See the [migration guide](migration.md#configuration) for configuration examples.

## Generated files

| File | Purpose |
| --- | --- |
| `MyApp.App.csproj` | Builds application code from the folder named by `RehostAppContentRoot`. Its `Compile` lines choose the files. |
| `MyApp.Host.csproj` | `RehostSiteContentRoot` names the application folder to copy. `OutDir` sends the binaries to `rehost_root/bin/`. |
| `Program.cs` | Starts the ASP.NET Core host using `AddRehostWeb` and `UseRehostWeb`. |
| `Web.Rehost.config` | Applies configuration changes to the application's copy. |
| `Properties/launchSettings.json` | Sets the development URL. |
| `.gitignore` | Keeps `rehost_root/` build output out of source control. |
