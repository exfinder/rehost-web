# Markdown documentation audit

Audit the current tracked Markdown tree. Report in chat; leave files unchanged.

## Scope

Read repository instructions and documentation authorities. Inventory every
included file; exclude generated, vendored, cached and transient documents.

Protect consumer documentation: root Markdown, public guides, application
READMEs, package/sample READMEs and published release notes. Their simplified
summaries are intentional. Report verified factual errors separately; changing
consumer prose requires explicit authorization.

`docs/dev` contains contributor documentation, except published release notes.
Application DEVELOPMENT files, internal commands and test/rig documentation
are also internal. Source-link and source-edit-rule corrections may cross the
consumer boundary only when explicitly authorized.

## Content policy

Current code owns mechanics; tests themselves provide evidence. Keep current
contracts, compatibility boundaries, non-obvious architectural rationale and
unresolved work. Remove historical measurements, transcripts, evidence IDs,
test inventories, completed plans and change ledgers. Transfer only material
needed for an unresolved decision into its follow-up before deleting an archive.

Imported-source metadata belongs in `docs/dev/sources.md`, one table:
`Import path | Upstream | Pinned revision | License`. Update only on imports or
upgrades. Keep license files and notices; code edits require no change ledger.

Honor ownership:

- Code/tests: mechanics and executable evidence.
- `PROJECT.md`: mission and contracts.
- `ROADMAP.md`: direction and milestones.
- `docs/dev/compatibility.md`: support claims and boundaries.
- `docs/dev/backlog.md`: unresolved-work index and priority.
- Follow-ups: unresolved contracts, decisions and completion criteria.
- ADRs: architectural rationale.

Backlog entries contain one-line outcomes and links to detailed follow-ups.
Delete completed follow-ups after moving any unique current contract or rationale
to its owner. Use brief context plus a link instead of repeating another owner.

## Audit and verification

Scan every included file for duplication, verbosity, staleness, contradictions,
unclear ownership, misleading headings and broken links. Inspect large files
and unresolved-work documents first. Check suspected stale claims against
current code, tests and configuration; history may help investigation but does
not belong in the resulting docs. Verify supported claims when evidence conflicts.

Run the repository documentation checker. Identify inbound links before proposing
a deletion, including links in protected docs. Prefer local links for repository
files and external links for upstream sources and issues.

## Report

Be concise. Group findings by `delete`, `merge`, `rewrite`, or `verify`; rank by
severity and tag `stale`, `duplicate`, `contradictory`, `unclear`, `verbose`, or
`structural`. For each, give exact file/line evidence, impact and the smallest
remedy. Report protected-document factual errors separately.

End with exact corpus inclusions/exclusions, coverage, finding counts and an
estimated reduction in lines, words and bytes.
