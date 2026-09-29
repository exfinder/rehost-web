# Katana source

The import authority is the official archived
[`aspnet/AspNetKatana`](https://github.com/aspnet/AspNetKatana) repository. The
local sibling checkout is `../AspNetKatana`, pinned at tag `v4.2.3`, revision
`ceea1fc0670221495b470f0b1ffcdd7526113a22`.

Imports:

| Upstream tree | Tree object | Files taken | Local destination |
| --- | --- | ---: | --- |
| `src/Microsoft.Owin.Host.SystemWeb` | `cb19ba15b1d524a42842d8bad9d5ce88857e8193` | 58 of 60 | `src/Rehost.Owin.Host.SystemWeb` |
| `src/Owin.Loader` | `82745d870dd9c258831870f1fd66e09b01b27f3b` | 3 of 7 | `src/Rehost.Owin.Host.SystemWeb/Loader` |

The sibling checkout is never a build input. Both trees are copied
byte-for-byte, keeping upstream directory layout, namespaces, and the
`Microsoft.Owin.Host.SystemWeb` root namespace, so `HttpContextExtensions`
still extends `System.Web.HttpContext` from the `System.Web` namespace. The
host tree drops only its `.csproj` and the `AspNetDictionary.Generated.tt`
template that produced a file already in the tree. From `Owin.Loader` the port
takes the three files upstream's project linked in — `Constants.cs`,
`DefaultLoader.cs`, `NullLoader.cs` — under `Loader/`, matching upstream's
`Link` paths; `LoaderResources` comes from the host tree's own
`App_Packages/Owin.Loader` copy.

Both `.resx` files keep their upstream logical resource names,
`Microsoft.Owin.Host.SystemWeb.Resources` and
`Microsoft.Owin.Host.SystemWeb.App_Packages.Owin.Loader.LoaderResources`, which
the SDK's default naming already produces from this layout. The names are what
the checked-in `.Designer.cs` files pass to `ResourceManager`.

Every source header declares Apache-2.0; the repository's `LICENSE.txt` is
preserved at `third_party/aspnet/AspNetKatana/LICENSE.txt`.

The port ships as the standalone `Rehost.Owin.Host.SystemWeb` package,
not inside `Rehost.Web`. It depends on
`Rehost.Web` plus the unmodified nuget.org `Microsoft.Owin` 4.2.3
and `Owin` 1.0.0 packages, consumed under `NU1701`. `Microsoft.Web.Infrastructure`
is not needed: Katana 4.x already calls `HttpApplication.RegisterModule`
directly, so the `PreApplicationStartMethod` attribute binds as imported.

The tree compiles on .NET 10 unmodified except for three files. The IIS-only
paths — `UnsafeIISMethods`, `ShutdownDetector`, `WebSockets`,
`OwinHttpHandler`/`MapOwinPath` — are kept as imported. Since
[ADR 0013](../adr/0013-integrated-pipeline-identity.md) the runtime answers
`HttpRuntime.UsingIntegratedPipeline` `true` and `HttpRuntime.IISVersion` 10.0,
so those paths no longer short-circuit, and ledger P92 supplied the two things
they then reached for: the host's stopping notification raises
`HostingEnvironment.StopListening`, which is what `ShutdownDetector` subscribes
to instead of polling, and the `WEBSOCKET_VERSION` server variable answers 13,
so `OwinAppContext`'s per-request detection no longer withdraws the
`websocket.Version` capability it advertises. `DisconnectWatcher` is the
imported file again: it reads `HttpResponse.ClientDisconnectedToken`, which
P92 made answer, so the refusal-latch fallback that stood here is gone.

One edit remains among those paths, because the identity promises what only
IIS's native plumbing delivers. `OwinCallContext.DisableResponseCompression`
takes its `SetKnownRequestHeader` fast path only when the worker request really
is an `IIS7WorkerRequest` — the reflected type, hoisted to a field and shared
with the delegate builder, tests the instance — because the compiled delegate
casts to that type and would otherwise throw `InvalidCastException` on the
host's own worker request. The fall-through branch removes `Accept-Encoding`
through `Request.Headers`, which refuses today.

`Loader/DefaultLoader.cs` loses `AssemblyDirScanner`'s `AppDomainSetup`
probing. Upstream derived its search paths from `PrivateBinPath` and
`PrivateBinPathProbe`, neither of which exists on modern .NET, and loaded each
candidate with `Assembly.Load(AssemblyName.GetAssemblyName(file))`. The
replacement searches `AppContext.BaseDirectory` and, when
`HostingEnvironment.IsHosted`, `HttpRuntime.BinDirectory` — together what
`PrivateBinPath` resolved to under ASP.NET — and loads by path with
`Assembly.LoadFrom`. The path load is required, not cosmetic: `Assembly.Load`
resolves by identity through the dependency graph, so an assembly sitting in
the scanned directory but outside that graph raises `FileNotFoundException`,
which the loop does not catch. The `BadImageFormatException` skip for native
files is unchanged. This scanner is only reached when `DefaultLoader` is
constructed without a referenced-assembly list; the System.Web host always
passes `BuildManager`'s list, so it is a fallback.

`CallEnvironment/TraceTextWriter.cs` loses its `kernel32!OutputDebugString`
P/Invoke, a `DllNotFoundException` off Windows the first time the OWIN pipeline
touches `host.TraceOutput`. `Write(string)` now calls `Trace.Write`, which
reaches the same `DefaultTraceListener` sink through managed code and appends
no newline of its own, so the `WriteLine` overload's explicit
`Environment.NewLine` is still the only line break. The `Debugger.IsLogging`
branch is untouched; the `DllImport` and its two code-analysis suppressions are
gone.
