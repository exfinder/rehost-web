# IIS integration: configuration subsystem and layered behaviors

Ledger P60 delivered the Layer-0 collection core, shipped `applicationHost`
baseline, and `staticContent` plus hidden-segment tenants with app-root
amendments. This file owns staged tenants and Layer-1 middleware. It is the
delivery vehicle for what the [IIS-role audit](iis-role-behaviors.md)
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
  Production configs rely on the prepend semantics (reading D8): a single
  added login page owns the root URL over the inherited list.
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
  `preCondition` filters". Built once, schema-true, with measured semantics:
  duplicate `add` is an error and removing an absent key is tolerated.
  `preCondition` evaluation: `integratedMode` →
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

## IIS module map

Navigation aid, not a runtime registry: each restored IIS-owned behavior sits
at a bespoke, readings-pinned position rather than behind a shared module
abstraction — the port's execution machinery is the classic managed engine,
not a rebuilt integrated pipeline (decision 0).

| IIS module | Behavior | Where it lives in the port |
| --- | --- | --- |
| `StaticFileModule` (native) | static transmit, content-type gate, revalidation 304s | P54/P58 inside the imported `StaticFileHandler`; types from the Layer-0 `staticContent` tenant (P60) |
| `RequestFilteringModule` (native), hidden segments | `404.8`-class refusal of hidden paths | P59 `ValidatePathExecutionStep` hook — before any pipeline event fires; Layer-0 `hiddenSegments` tenant |
| `DefaultDocumentModule` (native) | directory rewrite, courtesy 301, `403.14`-class refusal | P67 `DirectoryRequestExecutionStep` in `BuildSteps` after `PostResolveRequestCache`; Layer-0 `defaultDocument` tenant (readings D1–D15) |
| `DirectoryListingModule` (native) | directory listings; the 403.14 refusal shape when browsing is off | refusal folded into P67; listing itself is out of contract |
| `UrlRoutingModule` (managed, even on IIS) | route resolution | imported managed module, registration restored in the root configuration |
| `RequestFilteringModule` limits, `ProtocolSupportModule`/`customHeaders`, `httpErrors` shaping | pre-pipeline refusals and response-side shaping | staged — planned Layer-1 host middleware, per the tenant ledger |
| `ManagedEngine` (handler/module bridging) | integrated-mode `<handlers>`/`<modules>` registration | staged, own story |

## Tenant ledger (initial)

| Section | Classification |
| --- | --- |
| `staticContent` | honored — first tenant (gate + content types, replacing the P58 compiled table; app `mimeMap`/`remove`/`clear`) |
| `security/requestFiltering/hiddenSegments` | honored — migrate P59's compiled list |
| `httpProtocol/customHeaders` | honored — slice decision below |
| `defaultDocument` | honored — ledger P67; Layer-2 directory-request seam consuming the Layer-0 tenant (readings D1–D15, decisions 6–11 below) |
| `security/requestFiltering` limits | staged — three-way reconciliation with `maxRequestLength` and Kestrel. Production configs deliberately pair `maxAllowedContentLength` with an aligned `maxRequestLength`; the reconciliation must treat the pair as one intent, not refuse twice with two shapes |
| `httpErrors` | staged — governs Layer-1 error shaping; readings first. The observed production stance is `errorMode="Custom" existingResponse="PassThrough"` — IIS steps aside — which the port's app-shaped responses already satisfy; pin that mode first |
| `handlers`, `modules` | staged, own story — integrated mode ignored `system.web/httpHandlers` entirely, a genuine resolution fork from the port today; bridging design needs readings and its own plan. Production configs dual-register the same handler in classic `httpHandlers` and integrated `handlers`, so the classic engine honors the twin today; `modules` entries have no classic twin, making module bridging the urgent half (async session swap, third-party auth modules) |
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
1. **Foundation tenants**: `staticContent` plus the hidden-segments
   migration — two differently-shaped tenants prove the collection core;
   both behaviors are already pinned by shipped tests.
2. **Merge depth**: app root only; per-folder is an explicit recorded
   boundary whose trigger is the first real application that needs it. The
   Layer-0 accessor takes a path parameter from day one so adding the walk
   changes no consumer.
3. **Error shape**: P59's app-shaped 404 stays; the question belongs to the
   `httpErrors` story and is decided there with wire readings. Placement
   fact for that story: in the classic engine `ValidatePathExecutionStep`
   runs before the `BeginRequest` event, so the current seam is already the
   earliest possible *managed* placement — a dedicated module would run
   later, not earlier. The only genuinely earlier home is Layer-1 host
   middleware, which is the natural migration if the IIS error shape
   (404.8) wins.
4. **Handlers/modules**: staged end-state with its own later plan and
   readings; the expected forcing consumer is the session-state story
   (async session module registration lives in `<modules>`).
5. **Parked P60 draft**: discarded entirely; the foundation starts clean from this
   plan, regenerating data from the golden cache.

## Readings — default documents (taken 2026-08-14, IIS Express 10.0.26013 on winbox)

Rig: IIS Express (same w3core lineage as full IIS), integrated Clr4 pool,
scratch site with per-folder `web.config` variants as probe carriers. Schema
facts from the golden cache: `files` collection is `add`/`clear`/`remove`,
`value` `isUniqueKey`, `enabled` default `true`, and — load-bearing —
`mergeAppend="false"`. Golden server list, in order: `Default.htm`,
`Default.asp`, `index.htm`, `index.html`, `iisstart.htm`, `default.aspx`.

| # | Stimulus | Observation |
| --- | --- | --- |
| D1 | `/` with only `Default.aspx` on disk (list carries lowercase `default.aspx`) | 200, internal rewrite, no redirect. Page observes `Path`/`FilePath`/`CurrentExecutionFilePath`/`AppRelativeCurrentExecutionFilePath` spelled from the **config list** (`/default.aspx`), not the on-disk casing; `RawUrl` stays `/`; `Url` reflects the rewritten path |
| D2 | `/?q=1&x=%20y` | query string fully preserved through the rewrite |
| D3 | folder with `Default.htm` + `index.html` | `Default.htm` serves — strict list order |
| D4 | folder with only `index.html` (4th candidate) | serves — missing candidates skipped silently |
| D5 | `/sub` without trailing slash, directory exists | **301**, absolute `Location: http://host:port/sub/`, query preserved |
| D6 | directory with no candidate, browsing off | **403.14**, `DirectoryListingModule`, notification `ExecuteRequestHandler` |
| D7 | nonexistent directory `/nope/` | **404.0**, notification `MapRequestHandler` |
| D8 | folder `<add value="custom.htm">`, inherited candidate also present | `custom.htm` wins — **app adds prepend** (`mergeAppend="false"` confirmed on the wire) |
| D9 | folder `<remove value="index.html">`, file present | 403.14 — remove honored |
| D10 | folder `<clear />` + `<add>` | only the added value considered |
| D11 | folder duplicate `<add>` of an inherited value | **500.19**, `DefaultDocumentModule` — only directory requests in that folder fail; direct file requests in the same folder still 200 |
| D12 | folder `<defaultDocument enabled="false" />` | 403.14; direct file requests unaffected |
| D13 | duplicate `<add>` at the **app root** | all directory requests fail 500-class app-wide — including folders whose own `<clear/>` would discard the duplicate — while direct static and `.aspx` requests keep serving |

| D14 | slash-less URLs: nonexistent dir / existing empty dir / `enabled="false"` dir | `/nope` → **404, no redirect**; `/sub-empty` → **301** (redirect precedes candidate probing); `/sub-off` → **403, no redirect** — a disabled section suppresses the courtesy redirect too |
| D15 | duplicate `<add>` at app root, slash-less existing dir | **500, no redirect** — the broken section refuses before the redirect; direct file requests still 200 |

D11 + D13 with P60's C1 reconcile under one IIS model: a broken section fails
exactly the requests that consume it (`staticContent` is consumed by every
request, `defaultDocument` only by directory requests). D14/D15 pin the
module's internal order: read section → enabled gate → courtesy redirect →
candidate probe; only the dir-exists check precedes the section.

## Decisions — default documents (ratified 2026-08-14)

6. **Scope**: the full directory-request surface in one story — internal
   rewrite to the first existing candidate, 403-class refusal when none
   (directory browsing stays off/out of contract), and the 301 trailing-slash
   redirect with D5's shape (absolute `Location`, query preserved).
7. **Placement**: Layer-2 seam **after routing** — fires only for unrouted
   directory-mapped requests; the rewritten path then flows through normal
   handler mapping and P57 case folding. (Authorization runs before routing
   in the classic pipeline, so it evaluates the directory URL — matching the
   integrated-mode notification order, where auth precedes
   `MapRequestHandler`.) Matches measured
   ordering (routing at `PostResolveRequestCache`, default-doc work at
   `MapRequestHandler`/`ExecuteRequestHandler`); a route claiming `/` keeps
   winning, so Friendly URLs is unaffected.
8. **Casing contract**: candidate probing is case-insensitive on every
   platform via the P57 machinery; the app observes the **list's** casing
   (D1), `RawUrl` the original. Exercised on the CaseSensitiveDirectory
   fixtures plus a Linux round.
9. **Config-error model — recorded amendment**: `defaultDocument` alone
   adopts IIS's consumption-scoped failure (D11/D13): a broken section fails
   directory requests app-wide with a 500-class response and leaves direct
   requests serving. This deliberately deviates from the Layer-0
   fail-at-activation rule, which `staticContent` and `hiddenSegments` keep;
   the split is a ratified per-tenant exception, not drift.
10. **Baseline list**: verbatim golden six, order and casing untouched.
    `Default.asp`/`iisstart.htm` never exist in real apps, so probing skips
    them; keeping them preserves oracle-diffability.
11. **Error shape**: measured statuses now (301/403/404/500-class); bodies
    stay app-shaped per the P59 precedent. IIS-shaped bodies and subcodes
    remain the `httpErrors` story's decision — including the stub HTML body
    IIS attaches to the courtesy 301, which the port's redirect omits.

## Relationship to existing work

The [IIS-role audit](iis-role-behaviors.md) enumerates tenants; this plan
is how they land. P58/P59 stay as shipped; their data migrates into Layer 0
when their sections are honored, with tests unchanged — the tests pin
behavior, not data placement.
