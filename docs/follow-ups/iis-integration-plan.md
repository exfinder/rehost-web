# IIS integration: configuration subsystem and layered behaviors

Status: decisions ratified 2026-08-07; readings pass and slice 1 not yet
started. Priority: high.
Delivery vehicle for what the [IIS-role audit](iis-role-behaviors.md)
classifies as missing-worth-restoring; supersedes the ad-hoc shape P58/P59
used (compiled-in tables patched per story).

## Why this is infrastructure, not a feature

On Framework the observable contract was produced by System.Web *and* IIS
together, and applications configured IIS's half through
`<system.webServer>` — a section Framework's golden `machine.config`
declares as `IgnoreSection` ("belongs to IIS, not me"). Three restorations
have already landed piecemeal (P54 transmit path, P58 content gate + 304s,
P59 hidden segments), each with its own compiled-in data. A survey of a
production Web Forms application's `system.webServer` section shows what is
actually coming:

- `<handlers>` and `<modules>` — integrated-mode registrations with
  `remove`-by-name against inherited defaults and `preCondition` filters;
  the modules include third-party authentication and the async session
  provider. These types compile against System.Web — against *this port's*
  System.Web — so bridging them is realistic, and they are the heavyweight
  end-state tenants.
- `<security><requestFiltering><requestLimits>` — body and query-string
  limits that must reconcile with `system.web maxRequestLength` and
  Kestrel's own limit.
- `<httpProtocol><customHeaders>` — response headers IIS applied to every
  response, static and dynamic.
- `<defaultDocument>` — ordered candidate list with app-level additions.
- `<httpErrors>` — error-shape control (`errorMode`, `existingResponse`);
  IIS's refusals were IIS-shaped, not `customErrors`-shaped, and this
  section governed them.
- Sections that must be *silently tolerated no-ops*
  (`<validation validateIntegratedModeConfiguration>`, `<asp>`).

One-off adapters per section would re-create the pre-refactor test-suite
problem in configuration form. The foundation is built once; each behavior
becomes a tenant.

## Layer 0 — the IIS configuration subsystem

Request-agnostic; everything else consumes it, nothing else parses XML.

- **Golden reference**: verbatim `applicationHost.config` and
  `IIS_schema.xml` cached under `third_party/microsoft/iis-config/`
  (git-ignored, per the framework-config convention) with a committed
  `docs/iis-config-reference.md` recording versions and the re-pull recipe
  (win-oracle is disposable; the wire-rig `setup.ps1` installs IIS).
- **Shipped baseline**: a `rehost-webforms.applicationHost.config`-style
  artifact mirroring the subset of IIS-golden sections the port honors,
  standing to the IIS golden exactly as `rehost-webforms.web.config` stands
  to Framework's. Never merged into the Framework mirrors — they stay
  diffable against upstream.
- **Core model — the keyed collection**: IIS config is uniformly "ordered
  collection, keyed add/remove/clear, attribute defaults from schema,
  `preCondition` filters". Built once, schema-true, with IIS's strict
  semantics (duplicate `add` and `remove`-of-absent are errors — verified
  by probe, not assumed). `preCondition` evaluation: `integratedMode` →
  true, `runtimeVersionv4.0` → true, `managedHandler` → per-tenant policy.
- **Merge hierarchy**: shipped baseline → application root `web.config`.
  Per-folder `web.config` depth is a ratification decision (below).
  Atomic publication at activation; malformed content in an *honored*
  section fails activation naming the file and element; content in
  unhonored sections is ignored exactly as Framework ignored the whole
  group.
- **Typed accessors** per honored section; tenants never see XML.

## Layer 1 — host middleware ("native module stage")

For behaviors IIS decided before ASP.NET saw the request, from the raw
request alone: request-filtering limits, double-escaping and verb rules,
and (candidate) hidden segments. Faithful ordering — pre-auth,
pre-application — and faithful error surface: these refusals were IIS-shaped
(`httpErrors` governed), not application `customErrors`. Lives in
`Rehost.WebForms.Hosting` before the dispatcher, consuming Layer 0.
Explicitly *not* static serving or anything auth-dependent — the
two-authorities trap already rejected with `UseStaticFiles` (P58).

Response side of the same layer: `customHeaders` applied at the adapter's
commit, uniformly over static and dynamic responses.

## Layer 2 — in-pipeline seams

For behaviors that interleaved with the managed pipeline or need app state:
default documents (a rewrite that must then flow through handler mapping,
authorization, and P57 case folding), and the existing seams —
`StaticFileHandler` revalidation, the `DefaultHttpHandler` extension gate,
`MimeMapping`'s integrated arm — which re-source their data from Layer 0
instead of compiled-in tables. The `ValidatePathExecutionStep` hook (P59)
is this layer's precedent.

## Placement rule

For every future tenant: *did IIS decide it before ASP.NET saw the request,
without application state?* → Layer 1. *Did it interleave with the managed
pipeline or produce app-shaped responses?* → Layer 2. *Is it data an
existing System.Web mechanism already consumes?* → Layer 0 accessor feeding
that mechanism.

## Tenant ledger (initial)

| Section | Classification |
| --- | --- |
| `staticContent` | honored — first tenant (gate + content types, replacing the P58 compiled table; app `mimeMap`/`remove`/`clear`) |
| `security/requestFiltering/hiddenSegments` | honored — migrate P59's compiled list |
| `httpProtocol/customHeaders` | honored — slice decision below |
| `defaultDocument` | staged — needs its own readings (ordering, existence probing, interaction with directory requests) |
| `security/requestFiltering` limits | staged — three-way reconciliation with `maxRequestLength` and Kestrel |
| `httpErrors` | staged — governs Layer-1 error shaping; readings first |
| `handlers`, `modules` | staged, own story — integrated mode ignored `system.web/httpHandlers` entirely, a genuine resolution fork from the port today; bridging design needs readings and its own plan |
| `validation`, `asp` | tolerated no-ops, recorded |
| everything else | ignored silently, Framework-style |

## Readings (taken 2026-08-07, IIS 10.0.26100 on `win-oracle`)

Golden cache: `third_party/microsoft/iis-config/` (git-ignored;
[reference doc](../iis-config-reference.md)).

Schema (`IIS_schema.xml`): `staticContent` collection is
`addElement="mimeMap"`, `clearElement="clear"`, `removeElement="remove"`,
key `fileExtension` (`isUniqueKey`), `mimeType` required non-empty; the
section also carries a `clientCache` element (`cacheControlMode` default
`NoControl`, `setEtag` default `true`) — future tenant, out of slice.
`hiddenSegments` collection is `add`/`clear`/`remove`, key `segment`
(unique, non-empty).

Probes over the wire rig, app-level `web.config` against the served app:

| # | Stimulus | Observation |
| --- | --- | --- |
| C1 | duplicate `mimeMap` for an inherited extension | **500.19** — every request in the app fails on the config error |
| C2 | `remove` of an absent extension | **tolerated** — no error, requests unaffected. (Contradicts the strict-remove assumption the discarded draft coded.) |
| C3 | `clear` then `mimeMap .css` | `.css` serves; anything else answers **404.3** |
| C4 | `remove` of inherited `.css` | `.css` answers **404.3** — apps can restrict below the server list |
| C5 | app-level `<hiddenSegments><add segment="Private">` | **404.8**, case-insensitive — apps extend hiding |
| C6 | `<hiddenSegments><remove segment="App_Data">` | **App_Data serves** — delegation permits un-hiding; the tenant must honor `remove` |
| C7 | per-folder `web.config` `staticContent` in a subfolder | **works on IIS** (subfolder `.foo` serves with the folder's type; root unaffected) — the deferred per-folder boundary is real IIS behavior, recorded with evidence |

Subcodes for the future `httpErrors` story: 404.3 (content-type
restriction), 404.8 (hidden segment), 500.19 (config error), 404.0 (plain
missing).

Still deferred to the handlers story: integrated-mode handler resolution
order readings.

## Decisions (ratified 2026-08-07)

0. **Mode stance** (also anchored in PROJECT.md): the behavioral oracle is
   Framework under IIS integrated mode; the execution machinery is the
   classic managed engine. `UseIntegratedPipeline` sites resolve per-site —
   mechanism keeps the classic branch silently; app-facing contract sites
   (integrated-only events, `Response.Headers`, `TransferRequest`, module
   sequencing) resolve toward integrated-observable behavior with a ledger
   row each, catalogued by the audit.
1. **Slice 1 tenants**: `staticContent` plus the hidden-segments
   migration — two differently-shaped tenants prove the collection core;
   both behaviors are already pinned by shipped tests.
2. **Merge depth**: app root only; per-folder is an explicit recorded
   boundary whose trigger is the first real application that needs it. The
   Layer-0 accessor takes a path parameter from day one so adding the walk
   changes no consumer.
3. **Error shape**: P59's app-shaped 404 stays; the question belongs to the
   `httpErrors` story and is decided there with wire readings.
4. **Handlers/modules**: staged end-state with its own later plan and
   readings; the expected forcing consumer is the session-state story
   (async session module registration lives in `<modules>`).
5. **Parked P60 draft**: discarded entirely; slice 1 starts clean from this
   plan, regenerating data from the golden cache.

## Relationship to existing work

The [IIS-role audit](iis-role-behaviors.md) enumerates tenants; this plan
is how they land. P58/P59 stay as shipped; their data migrates into Layer 0
when their sections are honored, with tests unchanged — the tests pin
behavior, not data placement.
