# Golden IIS configuration reference

The pinned IIS configuration files, kept locally as the comparison baseline
for the [IIS integration plan](follow-ups/iis-integration-plan.md) — the
authority for what IIS contributed to the observable contract beside
System.Web.

Not committed: `third_party/microsoft/iis-config/` is git-ignored, because
these are verbatim Microsoft files. Pulled 2026-08-07 from
`C:\Windows\System32\inetsrv\config\` on `win-oracle` — Windows Server 2025
(10.0.26100), IIS 10, the same install the wire rig serves from. Re-pull
recipe: `eng/wire-rig/setup.ps1` installs the IIS features on a fresh box;
then `scp` the two files below.

## What each file answers

`applicationHost.config` is the server-level defaults: the
`<staticContent>` mimeMap (384 entries — the extension→type list the P58
gate and the Layer-0 static tenant mirror), `<hiddenSegments>` (P59's
list), default documents, request-filtering limits, the handler and module
default registrations the staged handlers story will need, and `httpErrors`.

`IIS_schema.xml` is the native config system's declarative schema: per
section, the collection shape (add-element name, key attribute,
clear/remove), attribute types and defaults — including defaults absent
from `applicationHost.config` itself. It is the closest thing to source for
the native parser and the authority the Layer-0 collection core is built
against.

Behavioral semantics the schema does not state (duplicate-add is an error,
remove-of-absent is tolerated, per-folder delegation) are measured by probe
against the live IIS on the same box; readings live in the
[plan](follow-ups/iis-integration-plan.md).
