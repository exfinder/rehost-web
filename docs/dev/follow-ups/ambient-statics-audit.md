# Ambient environmental statics audit

`HttpRuntime`, `HttpConfigurationSystem`, and `HostingEnvironment` carry
lazily-computed statics whose Framework implementations derive values from
machine state (install layout, registry, IIS). The bootstrap feeds each one
explicitly only when a consumer exists; the rest are dormant getters that
return Framework-shaped nonsense if a new consumer ever evaluates them.
`HttpConfigurationSystem.MachineConfigurationDirectory` was one: dormant since
import, first reached by the ASMX help page (2026-08-21), and its stale
fallback had also silently constant-hashed machine.config/root web.config out
of `BuildManager`'s compilation-cache invalidation.

Audit: sweep those classes for environment-derived getters and classify each
as (a) fed by the bootstrap, (b) dormant with no compiled consumer, or
(c) dormant with a reachable consumer. Feed or fail-fast group (c); leave (b)
listed so the next first-reach starts from the classification instead of a
symptom.

Done when the classification is recorded, every group-(c) getter is fed from
bootstrap-owned state or throws actionably, and a test pins each fed value
(the `SetConfigurationFilePaths` seam and the BuildManager special-files hash
have none yet).
