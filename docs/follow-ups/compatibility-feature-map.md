# Compatibility feature map

Status: open. Priority: high. Depends on explicit support decisions.

## Goal

Maintain a precise user-facing map of source/API and behavioral compatibility.
For each feature record: supported, partially supported, unsupported, or
unassessed; platforms; security/trust constraints; failure mode; and owning
contract/test.

## Rules

- “Supported” means portable tested behavior.
- Nothing is supported only on Windows.
- Partial support names the exact boundary.
- Unsupported behavior fails explicitly where reachable.
- Unassessed is not equivalent to unsupported or supported.
- Story completion updates the map; history stays in Git.

Implemented behavior remains canonical in feature contracts until this aggregate
map is complete.

## Done when

The first runnable request and every encountered IIS/Windows feature have
entries suitable for a future README compatibility section.
