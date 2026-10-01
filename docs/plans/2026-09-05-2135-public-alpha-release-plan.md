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

GitHub owns tasks and completion status in the
[Public alpha milestone](https://github.com/exfinder/rehost-web/milestone/1).
This document owns release scope; #8 preserves the decision. Issue bodies own
acceptance checklists. Do not copy them into the roadmap or backlog.

## Public-alpha contract

Decision record: [#8](https://github.com/exfinder/rehost-web/issues/8).
This fixes release scope, not current readiness. Evidence was inspected at
`3aaf93c6` and rechecked at `88f46bd4`, preserving the existing release-planning
edits. Maintainer confirmed the contract in #8. No builds or application journeys
were rerun for this decision.

### Distribution and version

Public GitHub source plus prerelease packages on nuget.org, with a tagged GitHub
prerelease. All seven packages below use `0.1.0-alpha.1`; internal package
dependencies must resolve to that same release. Third-party dependencies retain
their upstream versions. `0.1.0` is the existing version prefix; `1.0.0-local`
is repository feed machinery, never the public alpha version.

No inspected technical evidence requires source-only runtime distribution:
package-owned configuration, transitive runtime targets, and Hosting's bundled
XDT task already exist. External consumption is still unproven (#12), and
licensing remains unresolved (#9). Those are release gates, not reasons to
silently downgrade distribution. If either cannot be satisfied, reopen the
scope decision before calling a source-only delivery the alpha. App-specific
recompiles remain source-built examples.

### Agreed package boundary

The public alpha has seven NuGet packages:

| Package | Payload / Rehost package dependencies |
| --- | --- |
| `Rehost.Web` | Bundles the runtime, ApplicationServices, Extensions and Services assemblies, root configuration, runtime build assets and their external dependency closure |
| `Rehost.Web.AspNetCore` | Kestrel and staging/XDT integration; depends on `Rehost.Web` |
| `Rehost.AspNet.FriendlyUrls` | Friendly URLs; depends on `Rehost.Web` |
| `Rehost.AspNet.Web.Optimization` | Bundling/minification; depends on `Rehost.Web` |
| `Rehost.AspNet.Web.Optimization.WebForms` | Bundle controls; depends on `Rehost.AspNet.Web.Optimization` and the core bundle as needed |
| `Rehost.AspNet.ScriptManager.MSAjax` | Script-name mappings; depends on `Rehost.Web`; physical scripts remain application-owned |
| `Rehost.Owin.Host.SystemWeb` | Katana host replacement; depends on `Rehost.Web` |

This is package consolidation, not assembly merging or renaming. The four
component assemblies keep their identities and project boundaries; they are not
separately published NuGet packages. No package in the public graph may depend
on their unpublished package IDs.

Verified current implementation differs: [Rehost.Web.Package.csproj](../../src/Rehost.Web.Package/Rehost.Web.Package.csproj)
is a metapackage with four project/package dependencies, and
[LocalFeed.targets](../../eng/LocalFeed.targets) packs eleven packages.
[#12](https://github.com/exfinder/rehost-web/issues/12) owns consolidation
and external-consumer proof: include all four DLLs, embedded resources, root
configuration, direct/transitive build behavior and external dependencies;
reconcile the local feed and consumers without changing assembly identities.
[#16](https://github.com/exfinder/rehost-web/issues/16) verifies the final
seven-package graph, payload, symbols, notices and aligned versions.

Hosting continues to bundle its build task and XDT dependency; `eng/` projects
are not additional public packages. App-specific recompiles/shims remain source
examples: YAF libraries, OrmLite/dialects, `YAF.Compat`, its modified Web API host,
eShop's Autofac integration, Wingtip's WebRequest shim and AJAX Control Toolkit
recompiles. Existing upstream packages retain their own identities and versions.
[#9](https://github.com/exfinder/rehost-web/issues/9) inventories the actual
bundled contents and repository examples before licensing is settled.

### Onboarding and prerequisites

One predominantly managed C# Web Application Project, rebuilt with an App
library and a Kestrel Host executable targeting `net10.0`; one application per
OS process/current AppDomain, full trust. App references `Rehost.Web` and
needed feature packages; Host explicitly references `Rehost.Web` and
`Rehost.Web.AspNetCore`. Preserve original application inputs; apply XDT to
the staged copy. Explain rebuild-after-markup-edit behavior. Additional library
recompiles are manual compatibility work, not a broader onboarding promise.

- Reproduction baseline: .NET SDK **10.0.302**, `Microsoft.NETCore.App` and
  `Microsoft.AspNetCore.App` **10.0.10**. These were verified with `dotnet --info`
  on the inspection host; [global.json](../../global.json) permits
  `latestFeature` roll-forward, so it is not an exact SDK lock. #13 must pin the
  documented consumer reproduction; #16 records actual resolved SDK/runtime
  versions on every candidate platform. A changed baseline requires updated
  instructions and validation, not an inferred promise for every .NET 10 SDK.
- Required candidate matrix: Windows x64, Linux x64 and arm64, macOS arm64.
  Each architecture must pass independently. Current native Linux runner is
  arm64; historical generic Linux evidence does not prove both. #16 records exact OS version, Linux
  distribution/container digest and RID for each run; no blanket Linux
  distribution or older OS minimum is inferred from the current map.
- Basic stock template: no database, Docker, IIS or Visual Studio required for
  the modern App/Host run. SDK, package access and a browser suffice; Bash/curl
  are needed for the existing smoke script. Building the frozen Framework
  project itself is a separate Windows/Framework workflow.
- YAF default: PostgreSQL **17**, documented image `postgres:17-alpine`, database
  `yafnet` at localhost port **15432**. Docker is required for the documented
  container recipe; a separately provisioned equivalent PostgreSQL server can
  replace it. Installation needs an empty database and YAF's wizard; runtime
  needs writable application data for pickup mail and search. #16 records the
  image digest. No SQL Server or network SMTP is needed for the default journey.
- YAF SQL Server: optional historical alternative, with both the data-project
  reference and connection/provider configuration switched as documented.
  Only one backend may load. No SQL Server engine-version or native arm64
  support claim is established by that history; advertising it for this alpha
  would require exact prerequisites and candidate revalidation under #16.

The mandatory external-consumer promise is restore/build/run of the stock WAP
outside this checkout using candidate packages (#12/#13). Existing publish
commands are evidence of implementation, not closure of the open publish-payload
and designer-file policy. Document measured publish behavior; production publish,
containers, self-contained/single-file/trimmed/AOT deployment and Web Site
onboarding are not alpha promises.

### Advertised evidence and boundaries

[Compatibility](../compatibility.md) remains the sole support map. Alpha release
validation reruns the two featured examples below; other app records remain
historical compatibility evidence unless release material explicitly advertises
them as candidate-validated, in which case #16 must rerun them too.

- **Stock template quickstart:** default document and postback, Friendly URLs,
  `.aspx` redirect, static assets, script/style bundles, mobile master and view
  switching. [App notes](../../apps/WebFormsApplication/DEVELOPMENT.md).
- **YAF 3.2.16 richer example:** PostgreSQL installation, guest/member/admin
  authorization, registration with pickup-mail verification, OWIN sign-in,
  topic/reply/moderation, Forum Web API and Lucene search API (59 scripted
  checks). Repeat the separately documented manual process-replacement check
  with the same cookie jar and database to advertise cookie/content persistence;
  it is not part of `smoke.sh`. [App notes](../../apps/YAF/DEVELOPMENT.md) and
  [source deviations](../provenance/yafnet.md).

YAF is a modified, rebuilt fixture: preserve disclosure of source deviations,
its YAF/OrmLite/Web API sidecars and compatibility shims, Lucene package/namespace
substitution, OWIN replacement, async OEmbed call, C# 13/14 pins, database switch,
explicit machine key, pickup-mail adaptation and removed `system.net`
configuration. The sample machine key and credentials are fixture
inputs, not shared deployment defaults. The OEmbed media path itself is untested.

No YAF claim for image resizing/avatars (drawing paths are not portable),
attachments, private messages, multi-board creation, virtual-directory hosting,
upgrades or network mail. Only Forum and Search of ten Web API controllers are
exercised; search-page JavaScript is untested. SQL Server's earlier three-platform
journey is historical, not final PostgreSQL-candidate evidence.

Explicit exclusions: production readiness, multi-instance operation, arbitrary
application compatibility or binary interchangeability, further app ports,
general migration automation, Visual Basic and general API completeness. The
minimal WAP helper (#21) is optional. These exclusions do not waive #11's explicit
unsupported-security diagnostics or existing compatibility limits.

## Sequence and release gates

| Start | Work |
| --- | --- |
| Now, in parallel | [#9 License](https://github.com/exfinder/rehost-web/issues/9), [#10 publication audit](https://github.com/exfinder/rehost-web/issues/10), [#11 security settings](https://github.com/exfinder/rehost-web/issues/11), [#12 consumer packages](https://github.com/exfinder/rehost-web/issues/12) |
| Draft now; verify after #12 | [#13 Documentation and onboarding](https://github.com/exfinder/rehost-web/issues/13) |
| After code, packages, license and docs | [#16 Build and validate release](https://github.com/exfinder/rehost-web/issues/16) |
| With a prepared candidate | [#18 User trial and feedback](https://github.com/exfinder/rehost-web/issues/18); fixes return through #16 |
| After all required work | [#19 Publish](https://github.com/exfinder/rehost-web/issues/19) |

Repository visibility may precede packages: complete #9/#10 for the exposed
scope and obtain maintainer approval. Review material additions since the audit.
Package/tag publication requires #9, #10, #11, #12, #13, #16 and #18 plus
separate maintainer approval. Verify anonymous access and installation.
Private access or an isolated candidate feed is enough for the user trial.

The maintainer chooses the license under #9. Reporting routes and realistic
support expectations belong to #13/#18. Exact artifact and environment records
belong to #16. Preparation does not authorize publication, invitations or history
rewrites. Reopen the scope decision before changing the agreed release contract.

The [migration helper](https://github.com/exfinder/rehost-web/issues/21)
remains optional. Further ports, general migration automation and production
readiness remain outside alpha. Continuing feedback collection is not a release
gate.

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
