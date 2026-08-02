# `system.webServer` configuration compatibility

Status: open. Priority: high. Owns portable treatment of application
`system.webServer` configuration without introducing an IIS runtime profile.

## Goal

Inventory each reached setting and classify it as a portable host translation,
an existing System.Web equivalent, explicitly unsupported IIS/native behavior,
or proven inert input. Unassessed settings carry no compatibility claim.

Do not parse isolated settings in unrelated feature slices. Keep translation,
precedence, validation, diagnostics, and tests together. IIS-integrated pipeline
behavior remains excluded.

## First owned case

`security/requestFiltering/requestLimits/maxAllowedContentLength` is IIS
host policy, expressed in bytes and inherited from `applicationHost.config` or
an application `web.config`. Kestrel has a separate host-owned request-body
limit; `system.web/httpRuntime/maxRequestLength` remains System.Web-owned and
uses kilobytes.

Slice 4 keeps the Kestrel and System.Web limits independent, observes the
smaller effective limit, and leaves rejection with the owning layer. This
follow-up owns only the additional migration contract for legacy
`maxAllowedContentLength` input.

Decide:

- how an explicit application value maps to Kestrel;
- precedence against an explicit Kestrel limit;
- how inherited IIS values are supplied during migration;
- which conflicts fail before activation and what remediation they name; and
- which layer owns the rejection response.

No supported path may silently ignore the application setting, silently raise a
host security limit, or claim that Kestrel defaults reproduce deployed IIS
configuration.

## Done when

The encountered `system.webServer` vocabulary has an aggregate compatibility
map. Every translated setting has portable tests; every rejected setting fails
actionably; every inert setting has evidence that ignoring it preserves the
supported contract.
