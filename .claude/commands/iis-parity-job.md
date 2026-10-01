---
description: Run one integrated-vs-classic closure job (branch, plan, grill, implement, review, merge)
argument-hint: <job number 1-6>
---

Read ROADMAP.md "Current", the six-job plan in docs/dev/backlog.md, and the job
map + readings in docs/dev/research/integrated-divergence-audit.md. Take job
$ARGUMENTS (if no number was given, determine the next unfinished job from
the tracks earlier jobs leave — compatibility rows, ADR 0013, fences, tests —
and confirm the choice with me before starting).

Work pattern:

1. Create a branch for this job (`job-N-<slug>`) off main. All work happens
   there.
2. Write a short plan doc to the scratchpad: the job's items from the job
   map, agreed decisions, mechanics, tests, docs. Source facts from the audit
   doc; don't re-derive them. Pick the test kind from docs/dev/writing-tests.md's
   ladder: untouched Reference Source behavior gets a recorded run plus a
   compatibility row, not a standing test (rung 0).
3. If the job has an open decision, grill me first (one question at a time,
   each opening with a "port today vs .NET Framework" diff). Settle it before
   any code.
4. Hand the plan doc to an Opus subagent to implement on the job branch. It
   must read CLAUDE.md, docs/dev/code-style.md, and docs/dev/writing-tests.md first.
   It builds the solution, runs the full test suite, and commits its work to
   the branch (conventional-commit subjects, no Co-Authored-By).
5. Review stage: run the /compound-engineering:ce-code-review skill yourself
   from this session with `base:main`, not through a subagent (a wrapper
   agent backgrounds the reviewers and stalls). If the job changed only
   docs and tests, add `quick` to the arguments (one built-in pass); if it
   changed runtime code, run the full roster.
6. Judge the report yourself — do not apply it blindly. Check each finding
   against the plan, the audit's readings, and the code. Also do your own
   pass: comment audit (hidden-constraint comments only, 1-2 lines), no stray
   Console logging, tests fail against a stub. If you and the review disagree
   and you can't resolve it from evidence, grill me.
7. Fix accepted findings, commit the fixes to the branch.
8. Show me the final summary. On my OK: merge the branch into main.

Imported-source edits are fenced `#if !NETFRAMEWORK` with the original in the
other branch, plus a provenance entry in
docs/dev/provenance/reference-source.json. Behavior claims need a named winbox
reading (IV numbers in the audit) — never inference. Ask me before deviating
from anything agreed. One job only; stop when merged.
