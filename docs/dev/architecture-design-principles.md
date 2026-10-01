# Architecture design principles

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

- **Hotfixes stay minimal** — an unblocking fix ships the smallest change that unblocks; introducing a new seam or splitting an imported public method is an architectural decision to surface for approval before landing, not to document afterwards.