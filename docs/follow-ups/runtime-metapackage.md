# Runtime metapackage

**Resolved.** `src/Rehost.WebForms/` is the metapackage: no assembly of its own,
dependencies on the runtime, `ApplicationServices`, `Extensions`, and
`WebServices`, and ownership of the root configuration
(`configs/rehost-webforms.*.config` moved out of the runtime package). The
sample application references it plus its `packages.config`-era counterparts
and parses `asp:` tags with no companion named by hand.

Two mechanics worth remembering:

- The package that names companions in root configuration is the package that
  guarantees their presence. A runtime referenced on its own is not a complete
  distribution; project-reference consumers still get the configs copied from
  the runtime project's output.
- `build/` assets do not flow through package dependencies, so the runtime's
  consumer targets also pack as `buildTransitive/`. Without that, an
  application referencing only the metapackage compiles an empty app assembly.

## Original problem

Framework could name `System.Web.Extensions` and `System.Web.Services` in its
root `web.config` because the GAC guaranteed they were present. This port
splits the same surface into optional packages, so a named entry became a
promise the deployment does not keep. Both failures observed were that shape:
`System.Drawing.Common` in `<assemblies>`, and `Rehost.WebForms.WebServices`
in the same list once nothing referenced it. A `<controls>` entry is worse,
because prefix resolution loads every assembly registered for the prefix.

The runtime cannot supply the guarantee itself: `Rehost.WebForms.Extensions`
depends on it, so depending back is the cycle
[ADR 0009](../adr/0009-assembly-graph.md) exists to prevent.

## Still open

- Whether a minimal bundle (runtime + Extensions only) is worth adding beside
  the template-shaped one for size-sensitive deployments.
- Whether the host should fail at activation with a named diagnostic when
  configuration names an absent assembly, rather than per-page parser errors.
