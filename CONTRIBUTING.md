# Contributing

Read [PROJECT.md](PROJECT.md) first: it states what the port is, what it
refuses to be, and which sources decide behavior. [AGENTS.md](AGENTS.md) has
the commit and comment rules, [docs/code-style.md](docs/code-style.md) the C#
rules, [docs/writing-tests.md](docs/writing-tests.md) the test rules. This page
is the loop around them.

## Build and test

You need the .NET SDK 10.0.302 or a later 10.0.3xx release. Docker is needed
only for the applications with a database and for the Linux round.

```text
dotnet build Rehost.WebForms.slnx
dotnet test tests/Rehost.WebForms.Runtime.Tests/Rehost.WebForms.Runtime.Tests.csproj --no-build
dotnet test tests/Rehost.WebForms.Hosting.Tests/Rehost.WebForms.Hosting.Tests.csproj --no-build
dotnet test tests/Rehost.WebForms.Packages.Tests/Rehost.WebForms.Packages.Tests.csproj --no-build
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
  the shapes are in [docs/writing-tests.md](docs/writing-tests.md).
- **Evidence for behavior.** When Framework and the port could differ, the
  reading from .NET Framework 4.8.1 or IIS decides, and the change names it.
  Uncertain behavior is recorded in [docs/backlog.md](docs/backlog.md), not
  shipped as partial support.
- **Provenance for imported code.** Reference Source and other imported trees
  stay unchanged where practical; a needed edit is surgical and gets a row in
  [docs/provenance/](docs/provenance/).
- **Platform rounds.** A change validates on the current platform. A change to
  platform-dependent runtime logic passes on Windows x64, Linux and macOS arm64
  before it is done; [docs/cross-platform-validation.md](docs/cross-platform-validation.md)
  lists what counts. `eng/linux-round.sh` runs the committed HEAD in a
  container; [docs/windows-validation-host.md](docs/windows-validation-host.md)
  describes the Windows loop.
- **A conventional commit subject** (`type: summary`), a body only where the
  mechanism is not obvious from the diff, and no comments that explain what
  the code does or why the change is right.

Documentation is checked with `python3 eng/check-docs.py`. Contracts, rationale
and boundaries go in `docs/`; mechanics stay in code and tests.

## Support expectations for the alpha

This is a developer alpha maintained by volunteers.

- Bugs go through the [issue template](.github/ISSUE_TEMPLATE/bug_report.md);
  security reports go through [SECURITY.md](SECURITY.md), never an issue.
- Best effort, no response-time promise. An issue with a reproduction under
  `apps/` or a small project is looked at first.
- [docs/compatibility.md](docs/compatibility.md) states what is supported. A
  report against an unsupported or unassessed row is a scope request, and the
  answer may be a backlog entry rather than a fix.
- A fix ships as a new package set at one version, after the platform checks
  the change affects. There are no patches to an already published version.

## Shipping an alpha fix

1. Land the fix on `main` with its test. Bump `VersionSuffix` in
   `src/Directory.Build.props` (`alpha.1` to `alpha.2`); every package takes the
   new version, including ones that did not change, so internal dependencies
   keep resolving to one version.
2. Add `docs/releases/<version>.md` starting with `# <version>`: what changed,
   and the build record. The push to `main` runs `linux-x64.yml`; its `consumer`
   job packs the candidate and uploads it with `SHA256SUMS`.
3. Rerun the checks the change touches from that candidate, not from a local
   pack: `gh run download <run id> -n candidate -D candidate`, then
   `eng/external-consumer.sh candidate <version> <port>` and the affected
   application journeys on the platforms the change can reach (a path or
   filesystem change: all four; a docs or harness change: none). Record the
   results in the release page.
4. Run `publish.yml` by hand with the run id and the tag, `dry_run` on, and read
   the log. Then again with `dry_run` off. It pushes the packages and creates the
   GitHub prerelease from the release page.
