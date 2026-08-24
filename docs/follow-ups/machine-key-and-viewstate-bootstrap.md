# Machine key and ViewState bootstrap

The portable Framework-4.5+ algorithms and literal-key interoperability are
settled (ledger P15/P40/P48). Auto-generated keys remain process-scoped, so
ViewState, Forms Authentication, and Katana cookies fail across restart or
multiple instances. Explicit `<machineKey>` is the stable current contract.

## Isolation boundary

`,IsolateApps` and `,IsolateByAppId` overwrite key bytes with an application
hash. Framework and this runtime deliberately use different stable hash
algorithms (ledger P38/P48), so even identical literal keys with either suffix
cannot interoperate. Mixed-runtime deployments must use bare keys.

## Candidate design

ASP.NET Core Data Protection can provide a persisted, shared, rotating key ring
with application isolation. Feed its master material into
`MachineKeySection`, preserving ASP.NET wire formats; using an
`IDataProtectionProvider` only for Katana would split the crypto model and
leave ViewState unresolved.

This does not create Framework interoperability automatically: isolation
derivation and wire compatibility remain separate contracts. Coordinate with
[portable data protection](data-protection-provider.md).

## Open decisions

- Key source, persistence, rotation, sharing, isolation suffixes, and file
  permissions.
- Explicit configuration replacing registry-derived policy.
- Fail-closed behavior when secure key material is unavailable.

## Verification

Cover restart, multi-instance sharing, rotation, isolation, unavailable or
unreadable storage, and mixed-runtime literal-key vectors.

## Done when

Auto-generated keys have an approved persistent lifecycle; restart and
scale-out behavior is deterministic; failures are actionable; documented
Framework-interchange boundaries remain true.
