# Public alpha release plan
Created: 2026-09-05

## Objective

Release a public developer alpha for experimenting with migration of predominantly
managed C# ASP.NET Web Forms applications to modern .NET. Rebuild required;
compatibility bounded by `docs/compatibility.md`. Production readiness remains
Milestone 4 in `ROADMAP.md`.

This is a release preparation plan, not authorization to change repository
visibility, publish packages, or contact testers.

## Execution tracking

The [Public alpha milestone](https://github.com/exfinder/rehost-webforms/milestone/1)
owns task status. [Scope and distribution](https://github.com/exfinder/rehost-webforms/issues/8)
settles the remaining release decisions. The repo backlog links each issue;
this plan keeps sequencing without duplicate completion checkboxes.

1. Settle scope (#8); licensing (#9), publication audit (#10), configuration
   boundaries (#11), and consumer preparation (#12–15) can then proceed in
   isolated worktrees.
2. Produce candidate artifacts (#16), complete exact-candidate validation (#17),
   and run the external onboarding trial (#18). Before publication, a trial
   uses approved private access or candidate artifacts from an isolated feed.
3. Prepare feedback handling (#20), then launch (#19) after all mandatory
   gates and explicit authorization. Continuing feedback does not keep the
   release milestone open.
4. The [minimal migration helper](https://github.com/exfinder/rehost-webforms/issues/21)
   follows the verified walkthrough but remains outside the release milestone.

Readiness inspection at `3aaf93c6` confirmed YAF is landed: its README records
59 smoke checks, PostgreSQL as default, a prior three-platform SQL Server
journey, and separate manual restart evidence. Reuse this evidence for scope;
rerun the advertised journeys against the final candidate. No tests were run
as part of creating the tracking issues.

## Proposed release scope

- Public GitHub repository and tagged GitHub prerelease.
- Recommended: aligned NuGet packages at `0.1.0-alpha.1`.
  Distribution choice remains open; source-only is a narrower alternative.
- One small, reproducible C# quickstart and links to representative applications.
- Windows x64, Linux, and macOS arm64 support backed by release-candidate evidence.
- Explicit experimental status, compatibility limits, and support expectations.

Current evidence: `ROADMAP.md` records completed stock-template, eShop, and
Wingtip journeys on all three platforms. This planning session did not rerun
validation. Root README, root license, and checked-in GitHub workflows were absent
at inspection; package metadata exists in `src/PACKAGE_README.md` and associated
build configuration. Licensing blocks public package publication per
`docs/follow-ups/package-license.md`.

## Sequence and release gates

| Step | Work | Exit evidence |
| --- | --- | --- |
| 1. Publication audit | Review tracked files and Git history, fixtures, captured traffic, credentials, internal host details, imported source/assets, and GitHub-hosted content including Actions logs/artifacts. | No secrets or private material in the publication scope; discovered credentials rotated. Any history cleanup gets a separate, concrete decision before mutation. |
| 2. Licensing | Inventory imported source, generated/embedded assets, applications, and package contents; determine redistribution requirements; choose project license; add required licenses, notices, and package metadata. | Every distributed component accounted for; repository and package licensing resolved. |
| 3. Configuration boundaries | Close silent handling of native authorization, handler access policy, and impersonation; measure rewrite-rule handling and choose an explicit supported or refused boundary. Prefer narrow actionable refusals where support is outside alpha scope. | Configured protections cannot silently disappear; regression evidence on all target platforms and updated compatibility claims. |
| 4. Consumer quickstart | Provide one small C# app with exact prerequisites, package versions, host setup, and commands; explain recompile requirements and supported project shape. Exercise installation from disposable copies outside the checkout. | A new user can restore, run, and post back without repository-local feeds or undeclared assets. For source-only distribution, document and verify packing the required local feed explicitly. |
| 5. Candidate validation | Freeze candidate commit and package set; build and test Windows x64, Linux, and macOS arm64; run advertised application journeys. Record commands, commit, artifact versions, and results. Add reproducible release checks; investigate failures before publishing. | All three platforms pass on the release candidate; platform-specific branches execute where triggered. Linux/Windows rounds use committed HEAD as required by project instructions. |
| 6. Public-facing material | Add root README, quickstart, release notes, contribution guidance, security reporting instructions, and bug template. Link the authoritative compatibility map and backlog. | Audience, prerequisites, limitations, examples, reporting route, and experimental support expectations are clear. Security reporting destination is chosen and usable. |
| 7. Launch | Review the prepared publication scope; obtain explicit launch authorization. Change visibility, tag the validated commit, publish a GitHub prerelease and selected package artifacts, then verify anonymous access and installation. | Public instructions and artifacts work end to end; package contents match the validated candidate. |
| 8. Feedback | Prepare an invitation for a few migration testers; send only with authorization. Request minimal reproductions and platform/version details; triage failures against the compatibility contract. | Evidence from applications beyond existing fixtures informs subsequent alpha releases. |

Steps 1–2 determine what may be distributed. Steps 3–4 determine the candidate;
step 5 validates it. Documentation can proceed alongside preparation, but must
describe the final candidate. Launch follows every gate. No calendar deadline
is assumed; estimate after the publication and licensing inventories.

## Decisions to settle

- Distribution: GitHub plus NuGet recommended; source-only still open.
- License: maintainer choice informed by the complete redistribution inventory.
- Version: `0.1.0-alpha.1` proposed; confirm before fixing package dependencies.
- Security reporting destination and realistic maintainer support capacity.
- Existing-history publication versus a separately prepared public repository,
  only if the publication audit exposes material requiring that choice.

## Explicitly deferred

- Production orchestration, graceful lifecycle completion, and multi-instance support.
- General migration automation and broad API completeness; the bounded WAP
  helper is optional and cannot delay release.
- Visual Basic support and other capabilities outside the current contract.
- Production-readiness claims; Milestone 4 retains ownership of that work.

Deferral does not waive explicit errors for unsupported configured protections.
Any newly discovered release blocker is recorded in `docs/backlog.md`; milestone
changes belong in `ROADMAP.md`, not a competing roadmap here.

## Release containment

If candidate verification fails, hold publication and produce a new candidate.
If a public release proves defective, document the affected version, stop its
promotion, and publish a corrected prerelease. Do not assume reverting repository
visibility retracts already downloaded source or artifacts.

## References

- `PROJECT.md`, `ROADMAP.md`, `docs/compatibility.md`, `docs/backlog.md`.
- `docs/follow-ups/package-license.md`.
- `docs/bringing-up-an-application.md`.
- [GitHub repository visibility consequences](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/managing-repository-settings/setting-repository-visibility): public visibility exposes Actions history and logs as well as repository content.
- [NuGet prerelease versions](https://learn.microsoft.com/en-us/nuget/create-packages/prerelease-packages): suffixed versions identify prerelease packages.
