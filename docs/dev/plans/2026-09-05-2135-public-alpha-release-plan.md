# Public alpha release plan

## Scope and ownership

A developer alpha for predominantly managed C# Web Forms migration to modern .NET,
with rebuilt application assemblies and bounded [compatibility](../compatibility.md).
Production readiness belongs to ROADMAP's production baseline.

The [GitHub milestone](https://github.com/exfinder/rehost-web/milestone/1) owns tasks
and completion status; [#8](https://github.com/exfinder/rehost-web/issues/8) owns the
release decision. This document states the current contract and release gates.
Preparation does not authorize publication, invitations or history rewrites.

## Distribution

Public source, prerelease NuGet packages and a tagged GitHub prerelease. Use
`0.1.0-alpha.1` consistently within the package family; third-party packages keep
their own versions. `1.0.0-local` is repository feed machinery.

[PackageProjects.props](../../../eng/PackageProjects.props) owns the package set.
[Rehost.Web.Package](../../../src/Rehost.Web.Package/Rehost.Web.Package.csproj)
bundles the runtime, ApplicationServices, Extensions, Services and Infrastructure
assemblies with their configuration and dependency closure. They retain assembly
identities but are not independently published packages. Hosting bundles its
build task and XDT dependency; app-specific recompiles/shims remain source examples.

Candidate validation must use that current set, including Web API, Web Pages,
both ScriptManager packages and templates, rather than a copied package inventory.
No public package may depend on unpublished component package IDs. Source-only
runtime distribution requires reopening the release scope decision.

## Consumer contract

- One App library and Kestrel Host targeting net10.0, one application per process,
  full trust. App references the runtime and needed companion packages; Host adds
  Rehost.Web.AspNetCore. Apply XDT to a stage; preserve original inputs.
- Stock WAP restore/build/run must work outside this checkout using candidate
  packages. Explain rebuild-after-markup-edit and manual dependency recompiles.
- Document SDK/runtime prerequisites from global.json and the candidate's actual
  resolved environment. Candidate matrix: Windows x64, Linux x64 and arm64,
  macOS arm64; each architecture passes independently.
- Stock template needs SDK/package access and a browser; its existing journey
  uses Bash/curl. Framework builds are a separate Windows workflow.
- YAF uses PostgreSQL 17 on port 15432. Container instructions may be replaced
  with an equivalent server. Installation needs an empty database and its wizard;
  mail/search need writable application data. SQL Server is optional and needs
  backend-reference plus provider/connection changes; load one backend only.
- General publish, containers, self-contained/single-file/trimmed/AOT deployment
  and Web Site onboarding are not alpha promises. Publish/designer policy remains
  in [project models](../follow-ups/web-site-vs-wap-project-models.md).

## Featured examples and boundaries

Rerun the stock template and YAF on candidate packages. Any additional example
advertised as candidate-validated must also run under the release gate.

YAF is a modified, rebuilt fixture with YAF/OrmLite sidecars, compatibility shims,
Lucene packages, C# 13/14 pins, a PostgreSQL choice, explicit keys and pickup mail.
Describe its effective choices in [development notes](../../../apps/YAF/DEVELOPMENT.md)
and its imported revision/license in [sources](../sources.md). Keys and credentials
are fixture inputs, not deployment defaults. Check restart persistence by replacing
the process with the same database, key and cookie jar; the URL-only smoke cannot.

No YAF claim for portable image processing, attachments, private messages,
multi-board creation, virtual-directory hosting, upgrades or network mail. Forum
and Search are the reached Web API controllers; search-page JavaScript and OEmbed
media rendering remain unassessed. Optional SQL Server claims require their own
candidate environment and validation.

Exclusions: production readiness, multi-instance operation, arbitrary application
or binary compatibility, further ports, general migration automation, Visual Basic
and general API completion. The optional [WAP helper](https://github.com/exfinder/rehost-web/issues/21)
does not expand these promises. Unsupported security settings still need explicit
refusals.

## Release gates

| Gate | Required result |
| --- | --- |
| [#9 Licenses](https://github.com/exfinder/rehost-web/issues/9) | Candidate package license expressions, bundled notices and sample redistribution disclosures match actual contents; project code is MIT and imports retain their licenses |
| [#10 Publication audit](https://github.com/exfinder/rehost-web/issues/10) | Review exposed source, fixtures, workflow logs and additions before changing visibility |
| [#11 Security settings](https://github.com/exfinder/rehost-web/issues/11) | Unsupported configured security behavior refuses explicitly |
| [#12 Consumer packages](https://github.com/exfinder/rehost-web/issues/12) | External restore/build/run exercises package payload, resources and direct/transitive build behavior |
| [#13 Onboarding](https://github.com/exfinder/rehost-web/issues/13) | Instructions reproduce candidate consumption and state support/reporting expectations |
| [#16 Candidate validation](https://github.com/exfinder/rehost-web/issues/16) | Build the current package graph; validate payload, symbols, notices, aligned versions and featured journeys on the required matrix |
| [#18 User trial](https://github.com/exfinder/rehost-web/issues/18) | Trial a prepared candidate; fixes return through candidate validation |
| [#19 Publish](https://github.com/exfinder/rehost-web/issues/19) | Obtain separate maintainer approval after required gates; verify anonymous access and installation |

Documentation can be drafted alongside packaging but must verify the final graph.
Visibility may precede packages after license/publication checks and maintainer
approval. Private access or an isolated feed suffices for the user trial.

## Containment

Hold publication when candidate validation fails and prepare a new candidate.
For a defective public release, document the affected version, stop promotion and
publish a corrected prerelease. Reverting visibility does not retract downloads.
