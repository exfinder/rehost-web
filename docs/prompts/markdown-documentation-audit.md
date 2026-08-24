# Markdown documentation audit prompt

Audit this repository's Markdown documentation. Report only; do not edit docs.

## Scope

- Audit the current tracked tree.
- Include human-authored Markdown under the repository root, including root
  operational docs and application, sample, and test READMEs.
- Exclude generated, vendored, package-cache, analyzer-release, and transient
  report files.
- Scan every included file. Report only actionable findings; omit clean files.

Before auditing, read all applicable repository instructions and documentation
authority declarations. Inventory the corpus and state exact inclusions and
exclusions.

## Audit criteria

Assess:

- compactness and concision;
- content duplication;
- staleness;
- contradictions;
- clarity for a competent contributor new to the project;
- structural defects: broken links, orphan documents, unclear ownership or
  status, misleading names/headings, and terminology drift.

Use maximal lossless compression as the standard. Remove repetition and
historical narration, but preserve unique facts, rationale, citations,
provenance, unresolved decisions, and evidence. Archival documents receive the
same compression pressure, but a higher deletion threshold. Permit at most a
two-sentence contextual summary of material owned elsewhere, followed by a link
to its authority. Repeated lists, status, rationale, or plans are duplication.

Treat compression broadly: shorten prose, redesign structure, collapse repeated
setup, and replace exhaustive transcripts or classifications with tables when
the same evidence survives. Inspect the largest documents first, then run a
second pass specifically for:

- evidence logs repeating rig setup, requests, teardown, or conclusions;
- completed proposals and test plans whose landed contract is authoritative;
- overlapping inventory, portability, compatibility, and ledger summaries;
- long ledger/map rows that re-explain their linked evidence;
- repeated examples or exhaustive categories where representative cases and
  counts preserve the result.

When normalizing raw evidence, retain its ID, stimulus, observed result, exact
status/error where material, provenance, and contract conclusion. Stop when a
further cut would remove a unique boundary, decision rationale, reproduction
input, or contradictory observation.

Treat staleness as mismatch with current code, tests, configuration, roadmap,
or declared authority, not document age. Prioritize unresolved-work documents:
flag items already implemented, superseded, unsupported, no longer applicable,
or abandoned without roadmap relevance, owner, or evidence. Also flag
unverifiable temporal wording such as "currently," "soon," or "former."
Supported claims need lighter verification unless evidence conflicts.

Honor repository ownership:

- code and tests own mechanics;
- `PROJECT.md` owns mission and contracts;
- `ROADMAP.md` owns direction and priority;
- `docs/compatibility.md` owns support claims;
- `docs/backlog.md` owns unresolved work and priority;
- ADRs own architectural rationale.

Flag any competing owner. Distinguish a true contradiction from stale copying,
scope differences, or unclear ownership.

Prefer local links for files vendored or imported into this repository. Flag
GitHub links that effectively point back to a local file; retain external links
for upstream provenance, issues, prior art, or unvendored sources.

Keep `docs/backlog.md` and `docs/follow-ups/` only if they maintain a strict
index/detail split: backlog owns priority and a one-line outcome; a follow-up
owns only unresolved contract, evidence, and completion criteria. Recommend
deleting or merging completed follow-ups; Git preserves history.

## Verification

- Check every included file for editorial and structural issues.
- Verify every suspected stale or contradictory claim against current code,
  tests, configuration, and relevant Git history.
- Do not independently re-prove every supported claim.
- Run the repository's documentation checker when available.

## Report

Respond in chat only. Be extremely concise. Organize findings by action:
`delete`, `merge`, `rewrite`, or `verify`. Rank by severity and tag each finding
with applicable dimensions: stale, duplicate, contradictory, unclear, verbose,
or structural.

For every finding, provide exact file/line evidence, why it matters, and the
smallest concrete remedy. End with corpus coverage, finding counts, and an
estimated reduction in lines, words, and bytes. Do not create an audit file or
modify documentation.
