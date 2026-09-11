# Publication audit

Sanitized record for [#10](https://github.com/exfinder/rehost-webforms/issues/10):
what making the repository public would expose. Audited 2026-09-12. Values are
never quoted here; locations, categories and dispositions are.

## Scope

| Item | Reviewed |
| --- | --- |
| Repository | `exfinder/rehost-webforms`, private at audit time |
| Commit | `065d1f55` (`main` = `origin/main`), 708 commits of history |
| Refs that become public | `main` only: it is the sole remote branch; the remote has no tags |
| Local branches | Six exist. All are contained in `main` except `feat/alpha-packaging`, an active local branch (test changes only at audit time, not reviewed); #12 itself landed before the audited commit |
| Hosted content | 21 issues and 14 comments read in full; milestone 1; no releases, tags, workflows, Actions runs or artifacts; wiki, pages and discussions disabled; no repository secrets, variables or environments; two read-write deploy keys (admin-only, not exposed by visibility) |

Not covered: GitHub Projects (token lacks `read:project`), binary blob
contents (fonts, PNGs, `CatalogItems.zip`, `Thumbs.db`, `.snk`), and every
commit after `065d1f55`.

## Tools and method

- gitleaks 8.30.1, default rules, `gitleaks git --log-opts=--all` over the
  full history: 21 hits, all `generic-api-key`, all machine keys or one
  reference-source false positive (a field assignment).
- Targeted `git grep` over the tracked tree and `grep` over the complete
  `git log --all -p` output (198 MB): private IP ranges, hostnames, home
  directories, usernames, e-mail addresses, connection strings, password
  assignments, SSH and PEM key headers, GitHub, AWS, Slack and NuGet token
  shapes, AWS resource identifiers, cookie and bearer captures.
- File-name sweep over the tree and over every path ever added or deleted:
  key stores, certificates, captures, logs, dumps, database files, publish
  profiles, environment files.
- Upstream comparison by SHA-256 for every full strong-name key pair and every
  committed machine key that a fixture inherited (YAF `v3.2.16`, AJAX Control
  Toolkit sample site).
- Git identities across all refs; commit messages; issue bodies and comments.
- No discovered value was tested against any service. Detailed scanner output
  stays outside the repository in the session scratchpad with owner-only
  permissions.

## Findings

No publication blocker was found. D1 and D4 are resolved; D2, D3 and D5 are accepted as recorded. Nothing in the tree, history or hosted
content matches a real credential, private key, cloud identifier or customer
record.

### Needs maintainer decision

| # | Location | Category | Impact | Proposed action |
| --- | --- | --- | --- | --- |
| D1 | `docs/windows-validation-host.md` line 6 | Private LAN address of the Windows validation host | Low: RFC 1918 address, unreachable off the LAN | Resolved 2026-09-12: the current files name only the `winbox` alias and profile-relative paths (`~\...`, `$HOME`). The SSH user name stays by maintainer decision. History keeps the old text; a rewrite is not proportionate |
| D2 | Every commit; `LICENSE` | Author identity `Ex Finder` with a personal mail address on 1430 commit records | Already the public GitHub account identity | Accept. Changing it means rewriting all history |
| D3 | `docs/provenance/generated-build-inputs.json` lines 103–127; `docs/research/{blogengine-net,dnn-platform,wingtiptoys}-portability.md`; history of early docs and build logs | Absolute paths under the maintainer's home directory | Cosmetic: reveals a short local user name and sibling checkout names | Accept, or replace with relative wording in the four current files |
| D4 | `tests/Rehost.WebForms.ScenarioHost/fixtures/{auth,farm,postback}/web.config`; `apps/YAF/yafsrc/YetAnotherForum.NET/{Web,recommended.web}.config`; `apps/YAF/YAF.Host/Web.Rehost.config` | Explicit `machineKey` values generated for this repository (they differ from upstream YAF, which ships the element commented out) | None: maintainer confirmed on 2026-09-12 that they were generated for fixtures and never used on a real deployment | Resolved: intentional fixture, as #8 records. Do not reuse them outside fixtures |
| D5 | `eng/win-oracle.sh`, `eng/win-oracle.md`, `docs/windows-validation-host.md` | AWS rig automation and cost notes | Discloses that an EC2 oracle exists, its instance shape and idle policy; no account, AMI, key-pair, security-group or IP values are committed | Accept |

### Intentional fixtures

- Local container passwords in `apps/*/smoke.sh`, `apps/YAF/install.sh`,
  `apps/*/README.md` Docker commands and `apps/*/*.Host/Web.Rehost.config`
  connection strings. All point at `127.0.0.1` or LocalDb.
- Example accounts and addresses (`example.com`, `example.test`,
  `rehost.test`, `wingtiptoys.com`) in smoke scripts and fixtures.
- Seven YAF and ServiceStack `.snk` full key pairs under `apps/YAF/yafsrc/`:
  byte-identical to the public YAF `v3.2.16` sources. Two Microsoft
  `35MSSharedLib1024.snk` files are public-key-only.
- AJAX Control Toolkit sample `machineKey`: byte-identical to the public
  upstream sample site.
- Machine-key constants in `AutogenKeyStoreTests.cs`: sequential and
  placeholder strings.
- `apps/WingtipToys/**/Thumbs.db` and `FolderProfile.pubxml` (path
  `C:\inetpub\wwwroot\yaf`): carried from the upstream samples.
- Session-persistence golden capture `tests/parity/artifacts/golden/sessions.json`:
  no host, user or path fields.

## Material after this audit

The reviewed commit already contains the #9 outcome (`LICENSE`,
`THIRD-PARTY-NOTICES.txt`, package license metadata) and the #12 packaging
work. Everything below is outside the audited scope and needs the recheck
before visibility:

- Candidate packages and their build output; `artifacts/candidate/` is
  ignored and must stay so.
- NuGet or GitHub publication credentials introduced for #16 or #19: never in
  source, scripts, logs or issues.
- New issue comments, releases or Actions workflows created before the
  visibility change.

## Recheck procedure

Run from a clean checkout of the exact commit proposed for visibility.

```bash
gitleaks git . --log-opts="--all" --redact=100 --no-banner
```

```bash
git grep -n -i -E '192\.168\.|sshuser|BEGIN [A-Z ]*PRIVATE KEY|ghp_|gho_|github_pat_|AKIA[0-9A-Z]{16}|oy2[a-z0-9]{40}' -- ':!apps/YAF/yafsrc' ':!src/*ReferenceSource*'
```

```bash
git log --all --format='%an <%ae>' | sort -u
```

```bash
git ls-files | grep -i -E '\.(pfx|p12|pem|key|har|log|bak|mdf|env|publishsettings)$|known_hosts|id_rsa|secrets\.json'
```

Then compare the gitleaks hit list against the 21 known machine-key hits,
diff the tree against `065d1f55` for new fixtures or scripts, and read every
issue comment, release and workflow run added since 2026-09-12. Record the
rechecked commit in #10.
