@PROJECT.md

Read `PROJECT.md` completely.

When reporting information to me, be extremely concise and sacrifice grammar for sake of concision.

Use concise Conventional Commit subjects (`type: summary`). Add a body only
where the mechanism is not obvious from the diff. Existing commits carry long
bodies; do not treat them as the standard. Do not add `Co-Authored-By` footers.

For C# code outside imported Reference Source, prefer `var` over explicit local variable types.

DEFAULT: NO COMMENTS. A comment exists only to stop the next reader from
making a mistake — deleting a load-bearing line, "fixing" deliberate behavior.
Findings, justifications, and context from the current task go in the commit
message, provenance, or docs, never in code. Never write a comment that argues
the change is correct — that is PR-description content. Don't explain WHAT the
code does, and don't reference the current task, fix, or callers ("used by X",
"added for the Y flow"). Before committing, list every comment line the diff
adds; each either names a hidden constraint or gets deleted.

New test files mirror the folder of the source they cover, so a test for
`src/System.Web.ReferenceSource/Compilation/BuildManager.cs` belongs in
`tests/Rehost.WebForms.Runtime.Tests/Compilation/`. Existing flat test files stay
where they are.

A test that a stub implementation would also satisfy is not covering the
behavior. Prefer inputs that fail when the implementation degrades, and know
what the test looks like when it fails, not only when it passes.

How to choose the kind of test, the scenario act→assert shape, and the rest of
the test-authoring rules: [`docs/writing-tests.md`](docs/writing-tests.md).

## Architecture design principles

- **Explicit ownership over ambient discovery** — dependencies and lifecycle inputs come from the owning host/component, not process state, registry, environment quirks, or load location.

- **Deterministic behavior** — the same inputs produce the same result across machines and operating systems.

- **Cross-platform contract first** — supported behavior must work everywhere; platform-only behavior is either isolated or explicitly unsupported.

- **Validate before mutation** — resolve and validate all inputs before changing shared or global state.

- **Atomic state publication** — consumers observe either a complete valid state or no usable state, never partial initialization.

- **Simple lifecycle model** — prefer a small, explicit state machine over retries, implicit recovery, or complicated idempotence rules.

- **Fail fast with actionable errors** — invalid, conflicting, or unsupported use reports the exact problem near its source.

- **Recovery proportional to safety** — roll back local mutations when reliable; require process restart when global state cannot be safely reconstructed.

- **Immutable state after publication** — reduces races, hidden coupling, and uncertainty about which configuration consumers observe.

- **Small public API, deep internal implementation** — expose only stable intent; keep sequencing, compatibility machinery, and legacy integration internal.

- **Reuse authoritative mechanisms** — use existing parsers, validators, and runtime semantics instead of duplicating complex behavior.

- **Host-neutral seams over broad rewrites** — place narrow adapters around legacy assumptions, limiting change radius.

- **Surgical legacy modification** — change imported code only when evidence proves necessary; retain original behavior behind conditional compilation where useful.

- **Deployment is part of architecture** — required assets, defaults, copy rules, versioning, and packaging must be deterministic and tested.

- **Explicit scope boundaries** — defer uncertain behavior deliberately, document ownership, and avoid accidental partial support.

- **Durable architectural records** — preserve contracts, motivations, compatibility limits, and follow-up work in repository documentation.

- **Hotfixes stay minimal** — an unblocking fix ships the smallest change that unblocks; introducing a new seam or splitting an imported public method is an architectural decision to surface for approval before landing, not to document afterwards.

## Build diagnostics

Build the runtime project:

```text
dotnet build src/Rehost.WebForms.Runtime/Rehost.WebForms.Runtime.csproj
```

Run tests per project (or solution-wide — both are supported):

```text
dotnet test tests/Rehost.WebForms.Runtime.Tests/Rehost.WebForms.Runtime.Tests.csproj --no-build
```

### Codex CLI

If `dotnet build` hangs in Codex CLI, retry with `--maxcpucount:1`.

## Cross-platform validation

Windows x64, Linux, and macOS arm64 must pass before work is reported as
done. Defects so
far have appeared on only one of them: path separators, hidden-file
classification, and native libraries carrying the `.dll` extension off Unix.

A branch that exists to handle a platform difference must be exercised on the
platform that triggers it. A guard that has never executed is not a guard.

Case-sensitive-filesystem branches are triggered by a filesystem, not an OS:
the routine macOS suite exercises them on a disposable case-sensitive APFS
volume (`CaseSensitiveDirectory` fixtures; the same tests skip on
Windows/NTFS, which cannot express the situation). A story touching them also
runs one Linux round on the deployment-target OS: `eng/linux-round.sh`
(committed HEAD only, like a Windows round).

Cross-process synchronization uses a named `Mutex`. It is the only named
synchronization object supported on every target: named `EventWaitHandle` and
`Semaphore` throw `PlatformNotSupportedException` off Windows. Names carry the
`Local\` prefix, which is honored on both. A name derived from a string hash
must use a stable hash, since `string.GetHashCode` is randomized per process and
each process would otherwise take a different mutex (ledger P38).
