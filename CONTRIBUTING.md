# Contributing

Read [PROJECT.md](PROJECT.md) first: it states what the port is, what it
refuses to be, and which sources decide behavior. [AGENTS.md](AGENTS.md) has
the commit and comment rules, [docs/dev/code-style.md](docs/dev/code-style.md) the C#
rules, [docs/dev/writing-tests.md](docs/dev/writing-tests.md) the test rules. This page
is the loop around them.

For contracts, decisions, and evidence, see
[Contributor documentation](docs/dev/README.md).

## Build and test

You need the .NET SDK 10.0.302 or a later 10.0.3xx release. Docker is needed
only for the applications with a database and for the Linux round.

```text
dotnet build Rehost.Web.slnx
dotnet test tests/Rehost.Web.Tests/Rehost.Web.Tests.csproj --no-build
dotnet test tests/Rehost.Web.AspNetCore.Tests/Rehost.Web.AspNetCore.Tests.csproj --no-build
dotnet test tests/Rehost.Web.Packages.Tests/Rehost.Web.Packages.Tests.csproj --no-build
```

Build the solution before a `--no-build` test run: the hosting tests start a
separate scenario host, and a stale one fails in ways that look like runtime
bugs. A single test is selected with the Testing Platform syntax,
`-- --filter-method '*.The_Base_Directory_Is_The_Site_Root_With_A_Trailing_Separator'`, not `--filter`.

The applications under `apps/` build from packages packed out of `src/` into
a local feed on the first build:

```text
dotnet build apps/WebFormsApplication/WebFormsApplication.slnx
dotnet run --project apps/WebFormsApplication/WebFormsApplication.Host
apps/WebFormsApplication/smoke.sh http://127.0.0.1:5081
```

Every `apps/<App>/README.md` names its prerequisites and its journey.
`eng/external-consumer.sh artifacts/apps/feed 1.0.0-local` proves the packed
packages from outside the checkout, the way a consumer meets them.

## What a change carries

- **A test that is red without it.** A test a stub would also pass does not
  cover the behavior. Scenario tests run the real host in a separate process;
  the shapes are in [docs/dev/writing-tests.md](docs/dev/writing-tests.md).
- **Evidence for behavior.** When Framework and the port could differ, the
  reading from .NET Framework 4.8.1 or IIS decides, and the change names it.
  Uncertain behavior is recorded in [docs/dev/backlog.md](docs/dev/backlog.md), not
  shipped as partial support.
- **Imported source.** Keep needed edits surgical. Update the
  [source table](docs/dev/sources.md) only when importing or upgrading source;
  preserve upstream licenses and notices.
- **Platform rounds.** A change validates on the current platform. A change to
  platform-dependent runtime logic passes on Windows x64, Linux and macOS arm64
  before it is done; [docs/dev/cross-platform-validation.md](docs/dev/cross-platform-validation.md)
  lists what counts. `eng/linux-round.sh` runs the committed HEAD in a
  container; [docs/dev/windows-validation-host.md](docs/dev/windows-validation-host.md)
  describes the Windows loop.
- **A conventional commit subject** (`type: summary`), a body only where the
  mechanism is not obvious from the diff, and no comments that explain what
  the code does or why the change is right.

Documentation is checked with `python3 eng/check-docs.py`. Contracts, rationale
and boundaries go in `docs/`; mechanics stay in code and tests.

## Support expectations

Rehost.Web is maintained by volunteers.

- Bugs go through the [issue template](.github/ISSUE_TEMPLATE/bug_report.md);
  security reports go through [SECURITY.md](SECURITY.md), never an issue.
- Best effort, no response-time promise. An issue with a reproduction under
  `apps/` or a small project is looked at first.
- [docs/dev/compatibility.md](docs/dev/compatibility.md) states what is supported. A
  report against an unsupported or unassessed row is a scope request, and the
  answer may be a backlog entry rather than a fix.
- A fix ships as a new package set at one version, after the platform checks
  the change affects. There are no patches to an already published version.

## Shipping a fix

1. Land the fix on `main` with its test. The push to `main` runs
   `linux-x64.yml`; its `consumer` job packs the candidate at
   `0.1.0-alpha.<run number>` and uploads it with `SHA256SUMS`. Every package
   takes that version, including ones that did not change, so internal
   dependencies keep resolving to one version. `VersionPrefix` in
   `src/Directory.Build.props` changes only for a new release line.
2. Add `docs/dev/releases/<version>.md` starting with `# <version>`: what changed,
   and the build record. It can land after the candidate run; publishing reads
   it from `main`.
3. Rerun the checks the change touches from that candidate, not from a local
   pack: `gh run download <run id> -n candidate -D candidate`, then
   `eng/external-consumer.sh candidate <version> <port>` and the affected
   application journeys on the platforms the change can reach (a path or
   filesystem change: all four; a docs or harness change: none). Record the
   results in the release page.
4. Run `publish.yml` by hand with the run id and the tag, `dry_run` on, and read
   the log. Then again with `dry_run` off. It pushes the packages and creates the
   GitHub prerelease from the release page, tagging the commit the candidate run
   built.
