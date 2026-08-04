# .NET Framework semantic oracle

Isolated Slice 0 prototype. It targets `net481`; it is not referenced by the
production solution or runtime.

[`core-parity/sessions.json`](../core-parity/sessions.json) declares the
sessions both adapters run. A session is one process, one fixture, and an
ordered list of steps, each holding one or more requests issued together: the
host spawns itself once per session, activates a child ASP.NET application
through `ApplicationManager.CreateObject`, and a marshal-by-reference runner in
that application calls public
`HttpRuntime.ProcessRequest(HttpWorkerRequest)` for each request.

A fresh process per session is what makes "cold" mean cold. Requests after the
first in a session are warm by construction, and the shutdown notification
raised by `StopObject` lands in the session notebook.

The fixtures clear inherited configurable handlers/modules and register one probe
module and precompiled handlers. The default fixture serves synchronous,
asynchronous, identity-echoing, and deterministic request-body paths; the module
short-circuits `/complete` with `CompleteRequest`, so reaching that path's handler
at all is a failure signal.

A second fixture covers failure. It switches `customErrors` on, so responses are
the generic error page — fixed wording from the same imported resources, with no
stack trace or build footer — rather than the detailed page, whose contents
cannot agree across runtimes. Its paths throw from a module, throw from a
handler, and name a handler type that does not exist. That last mapping sets
`validate="false"` so resolution is deferred to the request and the application
still starts; validating it would test startup failure instead. Framework's
`HttpModulesSection.CreateModules` still appends its implicit
`DefaultAuthenticationModule`.

`Rehost.WebForms.Parity.Probes.dll` is not a host reference and is not copied beside
the host executable. Build staging places it only in the runnable fixture's
`app/bin`. Host startup asserts both properties.

The `netstandard2.0` session/observation contract, the session manifest, and the
probe/recording sources are shared with the portable parity adapter. Each runtime still
compiles its own `System.Web`-bound runner and probe assemblies.

The golden this produces is also what
[the adapter parity diagnostic](../adapter-parity/README.md) compares against.
That column reads its observation off the wire, so it compares a subset; the
excluded fields and the reason for each are listed there.

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
src/Rehost.WebForms.Parity.OracleHost/bin/Release/net481/fixture/app
```

## Run

```powershell
.\src\Rehost.WebForms.Parity.OracleHost\bin\Release\net481\Rehost.WebForms.Parity.OracleHost.exe run
```

JSON is written to standard output. Diagnostics go to standard error.

## Generate

```powershell
.\src\Rehost.WebForms.Parity.OracleHost\bin\Release\net481\Rehost.WebForms.Parity.OracleHost.exe generate `
  --output .\artifacts\generated\sessions.json
```

Review the generated trace and provenance. Promote it to
`artifacts/golden/sessions.json` only from the pinned Windows oracle
environment. The committed golden trace was produced this way; never hand-edit
its behavioral observation.

## Verify

```powershell
.\src\Rehost.WebForms.Parity.OracleHost\bin\Release\net481\Rehost.WebForms.Parity.OracleHost.exe verify `
  --expected .\artifacts\golden\sessions.json
```

Verification is byte-exact. The checked-in normalization manifest currently
contains no rules because no declared session captures an inherently
host-specific value. Add only narrow, justified field rules when a later session
proves one necessary; the current executable does not implement normalization
transforms.

## Captured observation

Per session: the session name, application-instance initialization as an
unordered bag, the ordered shutdown notification and instance count, and one
named observation per request. Per request:

- ordered module collection/initialization, handler, worker-response, and
  request-entry events;
- status and status description;
- ordered response headers;
- body bytes as Base64;
- logical flush sequence;
- escaped exception type/message/HResult/inner shape;
- `EndOfRequest` and completion counts.

The request-body session compares known-length, chunked-equivalent, preloaded,
classic, binary, buffered, bufferless, and bufferless APM managed outcomes.

No IIS site is required. Use IIS classic-mode probes separately only for
behavior this managed harness cannot reproduce.
