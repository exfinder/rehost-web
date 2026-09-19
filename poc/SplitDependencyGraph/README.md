# One dependency graph, two output folders

Standalone .NET 10 sample. No dependency on Rehost.WebForms; copy this entire
folder elsewhere to use it independently.

## Run

From this directory, with a .NET 10 SDK installed:

```sh
dotnet build --maxcpucount:1 --disable-build-servers -p:UseSharedCompilation=false
dotnet run --project Host --no-build -- --check
dotnet run --project Host --no-build -- --urls http://127.0.0.1:5080
```

The second command prints actual JSON operations, a shared-type identity check,
and loaded assembly versions/locations. The third serves the same probe at `/`.
You can also launch `Host/bin/Debug/net10.0/Host` directly (`Host.exe` on Windows).

Run the automated checks:

```sh
dotnet run --project Verify --no-build -- .
```

Verification builds disposable copies, compares ordinary flat and split builds,
checks package versions and actual assembly locations, exercises shared types,
nested references and French resources, repeats an incremental build, repairs a
deleted output, starts a copied deployment from an unrelated working directory,
calls its Kestrel endpoint, verifies that removing `localPath` breaks startup,
checks Release build/clean, publishes twice into one folder and starts the
result, publishes self-contained for the current runtime identifier, and proves
a direct lower Newtonsoft.Json reference fails with NU1605. Commands have timeouts; builds use one MSBuild
node with build servers and shared compilation disabled.

## Dependency graph

```text
Host (Microsoft.NET.Sdk.Web)
├── System.Text.Json 10.0.12
├── HostPackages
│   ├── Newtonsoft.Json 13.0.3
│   └── Humanizer.Core 2.14.1
├── Proj1 ── Proj2 ── NuGet.Versioning 6.14.0
└── App (Microsoft.NET.Sdk)
    ├── Newtonsoft.Json 13.0.4
    ├── System.Text.Json 9.0.20
    ├── Proj1 ── Proj2 ── NuGet.Versioning 6.14.0
    └── Proj3 ── Microsoft.Extensions.DependencyModel 9.0.0
```

Host receives the older Newtonsoft.Json requirement transitively through
HostPackages. It has no direct Newtonsoft.Json reference. Host's normal restore
selects **Newtonsoft.Json 13.0.4** and **System.Text.Json 10.0.12**.
HostPackages and App still compile against their own restored graphs; the Host
deployment uses Host's selected versions, as in an ordinary flat build.

The sample disables .NET 10 package pruning so the deliberate System.Text.Json
package requirements remain visible. Shared-framework conflict resolution still
applies normally: framework-provided assets need not be copied, and an installed
framework can supply a newer System.Text.Json. Verification compares with the
ordinary flat build on the same runtime.

## Output

```text
Host/bin/Debug/net10.0/
├── Host[.exe]
├── Host.dll
├── Host.deps.json
├── Host.runtimeconfig.json
├── HostPackages.dll
├── Humanizer.dll
└── rehost_root/bin/
    ├── App.dll
    ├── Proj1.dll
    ├── Proj2.dll
    ├── Proj3.dll
    ├── Newtonsoft.Json.dll                  # 13.0.4
    ├── System.Text.Json.dll                 # 10.0.12
    ├── NuGet.Versioning.dll
    ├── Microsoft.Extensions.DependencyModel.dll
    └── fr/Proj3.resources.dll
```

Symbols and standard SDK metadata are omitted from the diagram. Referenced
projects retain their normal intermediate/build directories; the diagram is
Host's runnable output.

Every dependency reachable from App in **Host's resolved graph** belongs in
`rehost_root/bin`, including dependencies also used directly by Host. Each asset
has one deployed copy. App's independently resolved output is not used as a
second deployment graph.

## Build integration

Host imports [`build/SplitOutput.targets`](build/SplitOutput.targets) and sets
`SplitApplicationProject`. The targets build a separate MSBuild task assembly
against the current SDK's MSBuild assemblies; that tooling project never enters
Host's runtime graph.

1. [`ComputeSplitLayout`](build/ComputeSplitLayout.cs) reads Host's
   `project.assets.json` with `System.Text.Json`. It traverses App's
   closure in the selected target and classifies the SDK's already-resolved
   `ReferenceCopyLocalPaths` using package/project metadata.
2. It adjusts `DestinationSubDirectory`/`DestinationSubPath` on those items.
   The SDK performs copying, incremental repair, stale-output cleanup and clean.
   Existing subdirectories, including satellite cultures, are preserved.
3. [`RewriteSplitDependencies`](build/RewriteSplitDependencies.cs) adds
   `localPath` to the corresponding entries in Host's generated `.deps.json`.
   It preserves selected versions and dependency edges, and fails if an App
   manifest asset cannot be matched to an SDK copy item.

Publish takes package files from a second SDK item list,
`_ResolvedCopyLocalBuildAssets`, so the same task relocates that list too. It is
a private SDK name; the publish checks in `Verify` fail if it stops working.
Publish reuses the build's `.deps.json`, so the `localPath` entries carry over.

For example, Host's Newtonsoft.Json runtime asset contains:

```json
"lib/net6.0/Newtonsoft.Json.dll": {
  "assemblyVersion": "13.0.0.0",
  "fileVersion": "13.0.4.30916",
  "localPath": "rehost_root/bin/Newtonsoft.Json.dll"
}
```

.NET 10's native host understands this path before managed code starts. There
are no module initializers, assembly-resolution callbacks, extra probing paths,
or custom assembly load contexts.

`SplitOutputSubdirectory` changes the output-relative destination. Paths outside
Host's output are rejected. `SplitOutputEnabled=false` selects a flat build for
comparison; clean before changing that switch on an existing output.

## Scope

This sample implements **multi-file, untrimmed `dotnet build` and
`dotnet publish`**, framework-dependent or self-contained. Single-file, AOT and
trimmed publishes fail explicitly: they write their own dependency manifest,
which the rewrite step does not reach.

The demonstrated assets are managed assemblies, project symbols and satellite
resources. The tasks also map SDK native/runtime-target entries, but this sample
does not exercise a native package. Content files and package-specific custom
copy targets keep their normal SDK/package behavior; this is not a generic
relocator for arbitrary files. Code that inspects `Assembly.Location` or assumes
all files are beside the executable can observe the changed layout.

## Sources

- [NuGet dependency resolution](https://learn.microsoft.com/en-us/nuget/concepts/dependency-resolution)
- [.NET 10 runtime implementation of localPath](https://github.com/dotnet/runtime/pull/118297)
- [Later SDK support for emitting localPath](https://github.com/dotnet/sdk/pull/50120)
