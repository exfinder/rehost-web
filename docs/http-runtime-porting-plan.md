# Historical HttpRuntime portability plan

Status: superseded by
[managed Web Forms runtime port plan](core-runtime-port-plan.md).

The earlier plan established useful principles:

- retain `HttpRuntime.Init`, `HostingInit`, and `FirstRequestInit` ordering;
- define phase postconditions;
- replace platform dependencies at leaves;
- test ordering and partial failure.

Its execution direction is no longer current:

- initial compatibility is classic managed pipeline, not IIS integrated mode;
- application System.Web configuration is not preflighted separately;
- the first executable slice is a configured precompiled handler, not dynamic
  `.aspx`;
- initialization-looking errors are classified by Framework ownership rather
  than made uniformly terminal.

Current sources:

- [canonical port plan](core-runtime-port-plan.md);
- [classic managed runtime model](classic-managed-runtime-model.md);
- [classic-path portability ledger](portability-ledger.md).
