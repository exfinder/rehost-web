---
status: accepted
---

# Separate host registration from application activation

The host first creates an application runtime from immutable explicit options
without mutating System.Web global state. The first routed request then performs
the single activation attempt: host-contract validation, legacy AppDomain-slot
binding, retained `ApplicationManager`/`HostingEnvironment` initialization, and
state publication.

The option normalization, explicit configuration mapping, host-owned asset
validation, and reversible binding work introduced by `fcc0372` remains useful.
Its eager System.Web configuration preflight does not. Public static
`WebFormsApplication.Initialize` no longer represents the lifecycle.
