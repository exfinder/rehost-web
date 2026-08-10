# Runtime metapackage

Framework could name `System.Web.Extensions` and `System.Web.Services` in its
root `web.config` because the GAC guaranteed they were present. This port
splits the same surface into optional packages, so a named entry became a
promise the deployment does not keep. Both failures observed so far are that
shape: `System.Drawing.Common` in `<assemblies>`, and `Rehost.WebForms.WebServices`
in the same list once nothing referenced it. A `<controls>` entry is worse than
an `<assemblies>` entry, because prefix resolution loads every assembly
registered for the prefix, so one missing entry fails `<asp:Label>` as surely as
the control it was added for.

The proposal is a `Rehost.WebForms` package carrying no assembly of its own and
depending on the runtime and its companions. Applications reference it instead
of naming companions one by one, which is closer to Framework, where an
application referenced nothing at all. The root configuration then belongs to
that package rather than to the runtime: whoever names an assembly should be
whoever guarantees it, and a runtime referenced on its own is not a complete
distribution.

The runtime cannot supply the guarantee itself. `Rehost.WebForms.Extensions`
depends on it, so depending back is the cycle [ADR 0009](../adr/0009-assembly-graph.md)
exists to prevent.

Until the package exists, every consumer in this repository references the
companions it needs explicitly, and `<controls>` names `Rehost.WebForms.Extensions`
on that basis. An application referencing the runtime alone fails to parse any
`asp:` tag, which is the cost of the interim and the reason the package is worth
building.

Open questions: whether one package or a small family (a template-shaped bundle
against a minimal one) fits real applications better; whether the host should
fail at activation with a named diagnostic when configuration names an assembly
that is absent, rather than letting the parser report it per page; and what the
deployment size looks like once every application carries the companions it does
not use.
