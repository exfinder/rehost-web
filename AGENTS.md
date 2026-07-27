When reporting information to me, be extremely concise and sacrifice grammar for sake of concision.

For C# code outside imported Reference Source, prefer `var` over explicit local variable types.

Default to writing no comments. Only add one when the WHY is non-obvious: a hidden constraint, a subtle invariant, a workaround for a specific bug, behavior that would surprise a reader. If removing the comment wouldn't confuse a future reader, don't write it. Don't explain WHAT the code does, since well-named identifiers already do that. Don't reference the current task, fix, or callers ("used by X", "added for the Y flow", "handles the case from issue #123"), since those belong in the PR description and rot as the codebase evolves.

## Architecture design principles

- **Explicit ownership over ambient discovery** — dependencies and lifecycle inputs come from the owning host/component, not process state, registry, environment quirks, or load location.

- **Deterministic behavior** — the same inputs produce the same result across machines and operating systems.

- **Cross-platform contract first** — supported behavior must work everywhere; platform-only behavior is either isolated or explicitly unsupported.

- **Validate before mutation** — resolve and validate all inputs before changing shared or global state.

- **Atomic state publication** — consumers observe either a complete valid state or no usable state, never partial initialization.

- **Simple lifecycle model** — prefer a small, explicit state machine over retries, implicit recovery, or complicated idempotence rules.

- **Fail fast with actionable errors** — invalid, conflicting, or unsupported use reports the exact problem near its source.

- **Recovery proportional to safety** — roll back local mutations when reliable; require process restart when global state cannot be safely reconstructed.

- **Immutable state after publication** — reduces races, hidden coupling, and uncertainty about which configuration consumers observe.

- **Small public API, deep internal implementation** — expose only stable intent; keep sequencing, compatibility machinery, and legacy integration internal.

- **Reuse authoritative mechanisms** — use existing parsers, validators, and runtime semantics instead of duplicating complex behavior.

- **Host-neutral seams over broad rewrites** — place narrow adapters around legacy assumptions, limiting change radius.

- **Surgical legacy modification** — change imported code only when evidence proves necessary; retain original behavior behind conditional compilation where useful.

- **Deployment is part of architecture** — required assets, defaults, copy rules, versioning, and packaging must be deterministic and tested.

- **Explicit scope boundaries** — defer uncertain behavior deliberately, document ownership, and avoid accidental partial support.

- **Durable architectural records** — preserve contracts, motivations, compatibility limits, and follow-up work in repository documentation.
