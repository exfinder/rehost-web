# .NET Framework semantic oracle

Isolated Slice 0 prototype. It targets `net481`; it is not referenced by the
production solution or runtime.

The host activates a child ASP.NET application through
`ApplicationManager.CreateObject`. A marshal-by-reference runner in that
application calls public `HttpRuntime.ProcessRequest(HttpWorkerRequest)` for one
bodyless cold synchronous request.

The fixture clears inherited configurable handlers/modules and registers one
probe module and one precompiled handler. Framework's
`HttpModulesSection.CreateModules` still appends its implicit
`DefaultAuthenticationModule`.

`FrameworkOracle.Probes.dll` is not a host reference and is not copied beside
the host executable. Build staging places it only in the runnable fixture's
`app/bin`. Host startup asserts both properties.

Build staging also binds `compilation/tempDirectory` to an app-owned writable
directory under `fixture/temp`. This retains Framework code-generation setup
without requiring access to the machine-wide Temporary ASP.NET Files directory.

## Requirements

- Windows;
- .NET SDK selected by the repository `global.json`;
- .NET Framework 4.8.1 runtime;
- package restore access for
  `Microsoft.NETFramework.ReferenceAssemblies` `1.0.3`.

The executable verifies the installed Framework release key is at least
`533320`. It intentionally cannot run on modern .NET or non-Windows systems.

## Build

From this directory:

```powershell
dotnet build FrameworkOracle.slnx -c Release
```

The staged application is:

```text
src/FrameworkOracle.Host/bin/Release/net481/fixture/app
```

## Run

```powershell
.\src\FrameworkOracle.Host\bin\Release\net481\FrameworkOracle.Host.exe run
```

JSON is written to standard output. Diagnostics go to standard error.

## Generate

```powershell
.\src\FrameworkOracle.Host\bin\Release\net481\FrameworkOracle.Host.exe generate `
  --output .\artifacts\generated\cold-sync.json
```

Review the generated trace and provenance. Promote it to
`artifacts/golden/cold-sync.json` only from the pinned Windows oracle
environment. The committed golden trace was produced this way; never hand-edit
its behavioral observation.

## Verify

```powershell
.\src\FrameworkOracle.Host\bin\Release\net481\FrameworkOracle.Host.exe verify `
  --expected .\artifacts\golden\cold-sync.json
```

Verification is byte-exact. The checked-in normalization manifest currently
contains no rules because this scenario captures no inherently host-specific
values. Add only narrow, justified field rules when a later scenario proves one
necessary; the current executable does not implement normalization transforms.

## Captured observation

- ordered module collection/initialization, handler, worker-response, and
  request-entry events;
- status and status description;
- ordered response headers;
- body bytes as Base64;
- logical flush sequence;
- escaped exception type/message/HResult/inner shape;
- `EndOfRequest` and completion counts.

No IIS site is required. Use IIS classic-mode probes separately only for
behavior this managed harness cannot reproduce.
