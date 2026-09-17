# Codegen storage

Framework placed generated output under `Temporary ASP.NET Files` in the CLR
install directory, or under `<compilation tempDirectory>`, then let the CLR
append subdirectories derived from AppDomain identity. Neither leaf survives:
the install directory holds no codegen root and is not writable, and
`SetDynamicBase` and `DynamicDirectory` are inert outside .NET Framework.

The root resolves in order: the host `CompilationTempDirectory` option, the
`REHOST_WEBFORMS_COMPILATION_TEMPDIRECTORY` environment variable, configured
`tempDirectory`, then `~/.rehost-webforms/codegen` under the user profile
(amended 2026-09-17). The default is the portable analogue of `Temporary
ASP.NET Files`: outside the reclaimable system temp root, outside the
application, beside the machine keys of ADR 0010. It first sat beside the host
binaries, which the staged site layout places in the application's `bin`; the
`bin` tree feeds the top-level hash that decides reuse, so output written there
invalidated itself and every restart recompiled. A profile-less process fails
at boot naming the option and the variable, the same way the key store does.
An explicit root inside the application root is accepted and warns once
(event 14). Any two set sources that disagree fail at preflight as conflicting
ownership rather than one silently losing. An unwritable root fails naming the
path and the source that supplied it, instead of Framework's silent relocation
to `%TEMP%`. The resolved directory is reported once at startup (event 13).

The generation segment is sixteen hex characters of a digest of the
application directory, keyed on that directory rather than the host
application ID, so renaming a host label keeps the previous run's output.
Distinct applications sharing one root never share a segment.

## Reuse and reclamation

The path is stable across restarts, which is what makes Framework's own reuse
work: the top-level hash in `hash/hash.web` decides between reusing the previous
run's assemblies and wiping the directory. Nothing else is required for an
unchanged application to restart without recompiling.

Cross-process behavior stays as Framework defined it. `CompilationLock` guards
mutation, and a file that cannot be deleted because it is loaded gets a
`.delete` marker for a later run to sweep. Two processes sharing one segment is
supported the way an IIS recycle overlap was.

Reclamation therefore differs by operating system rather than by design: Windows
locks a loaded assembly so the marker path runs, while Unix deletes it outright
and the marker is never written.

## Deployment

Containers are the primary production target. The default root lives in the
container user's profile, so every cold start recompiles, and a read-only root
filesystem refuses it at boot. Both are answered by pointing the environment
variable (or the host option) at a mounted writable volume — the variable
exists so the operator can do this without rebuilding the image.

Reuse across image versions is not expected even then: `bin` and `App_Code`
timestamps feed the top-level hash, so a rebuilt image invalidates it.
