# IIS configuration tenants

Current support is in [compatibility](../compatibility.md). This file owns
remaining configuration scope and implementation placement.

## Placement

- Layer 0 parses and atomically publishes typed application configuration.
- Host middleware owns decisions before System.Web and response shaping outside it.
- The managed runtime owns behavior interleaved with routing, authorization and handlers.

Tenants consume typed state instead of parsing XML independently. Malformed
honored sections and unhonored elements fail activation naming the file and
section. `urlCompression` and `httpCompression` warn because the host provides
no compression. Filtering reuses the canonical request boundary.

## Open contract

| Area | Required decision or result |
| --- | --- |
| Per-path configuration | Merge folder `staticContent`, `defaultDocument`, `hiddenSegments` and `fileExtensions`; root behavior already ships |
| Location blocks | Resolve `<handlers>` in `<location>`, file versus folder paths, precedence with folder `web.config`, and module scope |
| Root locations | Merge `path="."` and `path=""` into application-root state before readers run |
| Inherited server limits | Decide how migration supplies `applicationHost.config` values not present in application files; explicit application body limits already configure Kestrel |
| Request filtering | Classify `headerLimits`, character switches, denied URL/query sequences and `filteringRules` |
| Native authorization | Enforce per-path `security/authorization` and `handlers accessPolicy` over static and managed content; both currently fail activation |
| Native verb responses | Decide `OPTIONS` 200 with `Allow`/`Public` and `TRACE` 501 versus the current 405 |
| Static validators | Support `clientCache setEtag="false"` without losing `Last-Modified`; current activation refuses it because the managed static handler owns ETag writes |
| Rewrite | Decide outbound/global rules, allowed server variables, wildcard patterns, substatus, lower scopes and cross-application targets; close parser divergences and the missing original-request log tail |
| Error responses | Assess per-path `httpErrors`, substatus matching, `ExecuteURL` and early middleware responses outside the spool |
| Dynamic caching | Reopen only for a public application without session cookies; preserve URL-keyed sharing, cached identity and cookie-triggered disablement |

## Done when

Each reached setting has one owner, an explicit support boundary, actionable
refusal or deliberate inert treatment, and tests at the layer making the decision.
