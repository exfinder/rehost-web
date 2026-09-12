# IIS configuration tenants

The Layer-0 configuration model and delivered tenants are recorded by ledger
P60/P67/P83/P85-P87 and P99-P101. This file owns only remaining IIS-derived
behavior.

## Placement rule

- Layer 0 parses and atomically publishes typed application configuration.
- Layer 1 host middleware owns decisions IIS made before System.Web and
  response shaping applied outside the managed pipeline.
- Layer 2 owns behavior interleaved with managed routing, authorization, or
  handler execution.

Tenants consume typed Layer-0 state; they do not parse XML. Honored malformed
sections fail activation naming the file and element. Unhonored sections remain
ignored.

## Open tenants

| Section | Required result |
| --- | --- |
| `security/requestFiltering` limits | Reconcile `maxAllowedContentLength`, `maxRequestLength`, query/URL limits, verbs, and Kestrel ownership without duplicate refusals |
| `httpErrors` | Define pre-pipeline error bodies and `existingResponse` interaction with managed `customErrors`. Reading D16: the directory-request 403 (D6) is answered natively and bypasses `customErrors`; the port throws it into the managed pipeline, so `customErrors` redirects instead |
| `staticContent`, `defaultDocument`, `hiddenSegments` below app root | Decide and test per-folder merge behavior; root behavior already ships |
| Remaining sections | Explicitly classify as future tenant or tolerated no-op |

`httpErrors` requires IIS readings before implementation.
Request-filtering behavior must reuse the canonicalized request boundary rather
than introduce a second path parser.

## Evidence

Historical collection, default-document, and `Response.Headers` readings are
preserved in [IIS integration readings](../research/iis-integration-readings.md).
The [IIS-role audit](iis-role-behaviors.md) owns the broader inventory.

## Done when

Each listed section has an evidence-backed support boundary, one configuration
owner, and tests at the layer where IIS originally decided it.
