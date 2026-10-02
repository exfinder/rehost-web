# Migration stages and coexistence

Current layout is in [migration reference](../migration-reference.md#app-and-host-layout);
project separation and output placement are decided in
[App/Host layout](../adr/0014-app-host-layout.md).

## Intended progression

1. Side-by-side: legacy source is input; the Host serves a staged copy without
   either runtime overwriting the other's bin.
2. Port-only: make rehost_root source-owned and remove the legacy project.
3. Coexistence: Core endpoints claim migrated URLs; Web Forms handles the rest.
4. Completion: remove the last Web Forms content, App project and Rehost packages,
   leaving the ASP.NET Core Host.

## Open contract

- Stage 1 edit-markup-refresh: [in-place development](in-place-dev-run.md).
- Stage 3 dispatch: UseRehostWeb is terminal, so choose an explicit fallback or
  partition. Define shared authentication-cookie and session-store bridges.
- Web Site source/publish ownership: [project models](web-site-vs-wap-project-models.md).
- Revisit separate Host/app binary placement only when SDK support removes the
  private deps-manifest/task machinery, or a coexistence consumer needs it.
  Preserve one dependency graph and assess compiled-page reuse after Host-only edits.

## Done when

The supported stages have explicit source/output ownership, coexistence preserves
URLs and shared state, and the final stage leaves an ordinary Core host.
