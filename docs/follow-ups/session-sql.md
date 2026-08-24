# Session state: SQL mode

Split from [session state](session-state.md), which owns InProc and Custom.

Host, container-image, and service observations below are a 2026-08-09
snapshot; verify them before running validation.

## What is already true

The client is transport-clean. `State/sqlstateclientmanager.cs`, 1,533 lines,
makes **zero** native calls — it is `SqlClient` throughout, and `SqlClient` is
already a shipped dependency. Nothing about this mode is structurally blocked,
which is what separates it from [state server
mode](session-state-server.md).

The provider choice is settled elsewhere and is not this story's to reopen:
`dependency-decisions.md:6-11` keeps legacy `System.Data.SqlClient` because
runtime public APIs expose its concrete types, and records that swapping to
`Microsoft.Data.SqlClient` needs its own ADR plus differential tests. This story
inherits it and pins the connection-string contract instead — in particular what
`Encrypt` and `TrustServerCertificate` must be against a modern server, which is
a contract to state rather than a setting to discover in a failing test.

## The schema is Microsoft's, and it is enforced

`InstallSqlState.sql` creates database `ASPState` holding the stored procedures,
with `ASPStateTempSessions` and `ASPStateTempApplications` in **tempdb** — so a
SQL Server restart drops session data and `CreateTempTables` re-runs from a
startup procedure. `InstallPersistSqlState.sql` is the persisted variant.

The store hard-appends the catalog — `sqlConnectionString += ";Initial
Catalog=ASPState"` (`sqlstateclientmanager.cs:237`) — and gates the schema: it
probes `sysobjects` for `TempGetVersion`, then calls `dbo.GetMajorVersion` and
refuses a mismatch (`sqlstateclientmanager.cs:1003-1029`). A hand-rolled schema
therefore fails closed and loudly. The procedure surface is versioned:
`TempGetStateItem`/`2`/`3`, `TempGetStateItemExclusive`/`2`/`3`,
`TempInsert*`, `TempUpdate*`, `TempRemove`, `TempResetTimeout`, `TempGetAppID`.

Those procedure bodies carry the real locking semantics — exclusive acquire,
lock cookies, expiry. Transcribing them is copying, not reimplementing.

## Provisioning

Microsoft's script is not redistributable, which is why
`third_party/microsoft/framework-config/` is git-ignored. This story follows the
same route: pull `InstallSqlState.sql` into a git-ignored `third_party` path and
record the source host, Framework release, and pull date the way
[framework-config-reference.md](../framework-config-reference.md) does. It is
present on `winbox` at
`C:/Windows/Microsoft.NET/Framework64/v4.0.30319/InstallSqlState.sql`.

Default to the tempdb flavor, which is `aspnet_regsql`'s own default. The
persisted variant shares the client path and is recorded as untested rather than
unsupported. `allowCustomSqlDatabase` defaults to false.

The consequence to own: **CI cannot self-serve.** A fresh machine needs access
to a Framework installation before these tests can run at all. That is the first
item for whichever infrastructure story picks this up.

## Test infrastructure

- `mcr.microsoft.com/mssql/server:2025-latest` is **amd64-only** — a
  single-architecture manifest, confirmed against the MCR registry. Apple
  Silicon runs it only under emulation. Microsoft's arm64 answer was Azure SQL
  Edge, which is retired and does not carry the full T-SQL surface these
  procedures need.
- At capture time, `winbox` had a working Docker engine and `win-oracle` did not.
- So the macOS leg is either emulation or an SSH tunnel to `winbox`'s engine.

## Evidence

Two hosts, one database, plus contention:

- **Two hosts.** Spawn two scenario host processes over one fixture pointed at
  the same `ASPState` database; write the session through host A and read it
  back through host B under the same session cookie. No in-memory store passes
  this, and it is the port-local stand-in for the farm case deferred below.
- **Contention.** Two concurrent requests for one session ID must serialize.
  This is what `TempGetStateItemExclusive3` and the lock cookie exist for, and
  it is the likeliest place a port diverges without anyone noticing.

`useHostingIdentity` is degenerate here for the reason recorded in
[session state](session-state.md): P07 leaves impersonation inert, so the store
connection is always opened as the process identity.

## After the validation rounds, these tests are skipped

Decided deliberately: once a few rounds pass on macOS and `winbox`, the
store-backed tests are marked skipped unconditionally, and re-enabling them is
infrastructure work owned separately.

The accepted cost, stated plainly so nobody rediscovers it as a surprise: the
tests then execute nowhere, including on a machine with the container already
running, and a regression in the SQL store is invisible until that
infrastructure story lands. The repository's usual position is that a guard
which never executes is not a guard; this is a knowing exception, not an
oversight. The `Skip` reason should name what has to be running for the test to
be meaningful, so re-enabling is mechanical.

## Deferred: interop with live Framework nodes

Sharing one `ASPState` database with running Framework nodes — write on 4.8.1,
read here, and the reverse — would extend the mixed-farm contract recorded in
[the compatibility map](../compatibility.md#state-security-and-ancillary-assemblies). It
is deliberately **not** in this story, for two structural reasons:

- The parity harness compares golden traces from two *independent* runs;
  `sessions.json` is process-per-session with no shared external state.
  Cross-runtime interop is a different shape — sequenced runs against one store,
  a session cookie carried between runtimes, and assertions about stored bytes
  rather than trace equality. That is a new coordination mode, not an extension
  of `TraceComparer`.
- It can only run where a Framework runtime and the database coexist, which in
  the current fleet is `winbox` alone: macOS has no Framework, `win-oracle` has
  no Docker engine.

A cheaper intermediate exists if the full harness is not wanted: capture what a
Framework node writes into `ASPState` once, commit it as a golden blob, and
assert port-local that the port reads it and that its own writes match. That
proves format agreement while leaving live lock and expiry contention between
two runtimes unproven.

## Done when

- An application configured `mode="SQLServer"` reaches a provisioned `ASPState`
  database and the preflight refusal in [session state](session-state.md) is
  removed.
- The port-local two-host and contention claims pass on Windows x64, Linux,
  and macOS arm64 before the tests are skipped.
- The provisioning route, its Framework-machine dependency, and the skip
  decision are recorded where an infrastructure story can pick them up.
