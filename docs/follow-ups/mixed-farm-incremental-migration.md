# Mixed farm and incremental migration

Status: the Framework-to-port direction is gated by
`MixedFarmOverKestrelTests` against a payload captured from .NET Framework
4.8.1. The reverse direction is measured, with an on-demand verification
procedure decided under ADR 0045 (see "Done when"). Priority: high — this is a
deployment capability future consumers depend on.

## The scenario

A Web Forms application is moved to this port one node at a time. Framework
nodes and rehosted nodes sit behind one load balancer sharing an explicit
`<machineKey>`, and a client's postback may land on either. The alternative is a
big-bang cutover of an application whose whole point is that it is too valuable
to rewrite.

That is ordinary web-farm behavior — a farm works because every node shares the
key, so no node depends on having rendered the page it is now receiving. What
makes it worth recording is that the two nodes are *different runtimes*.

## What is proven

A page carrying a server form, rendered on one runtime and posted to the other,
with both declaring the same literal `<machineKey>`:

| direction | result |
|---|---|
| Framework `__VIEWSTATE` posted to the port | accepted, `IsPostBack` true |
| the same payload with one character flipped | rejected, MAC failure suppressed |
| port `__VIEWSTATE` posted to Framework | accepted, `IsPostBack` true |
| the same payload with one character flipped | rejected, MAC failure suppressed |

The tamper controls are what give the positives meaning: a payload that fails
MAC validation renders the page as if freshly requested, so acceptance can only
mean validation passed and the payload decrypted.

A full button postback — all three hidden fields plus the button's own
`name=value`, as a browser sends them — behaves the same way:

| direction | result |
|---|---|
| Framework payload posted to the port | 200, the button's `Click` handler ran |
| the same payload with `__EVENTVALIDATION` removed | 500, invalid postback argument |
| port payload posted to Framework | 200, the button's `Click` handler ran |
| the same payload with `__EVENTVALIDATION` removed | 500, invalid postback argument |

The removal controls matter for the same reason: they show event validation was
being enforced on both sides, so the handler running cannot be explained by
validation having been off. `__EVENTVALIDATION` is therefore interchangeable too,
which is what makes this a real postback rather than a bare payload.

The Framework-to-port row and its control are now
`MixedFarmOverKestrelTests`, replaying
[`fixtures/farm/Framework.postback`](../../tests/Rehost.WebForms.ScenarioHost/fixtures/farm/Framework.postback)
over a real socket. Giving the fixture a key the capture was not made under
fails it, which is the assertion that the *shared key* — not merely a
well-formed payload — is what makes the farm work.

Content crosses too, not merely the signature. The fixture's `Page_Load` assigns
its label only when `!IsPostBack`, so the value present after a cross-runtime
postback can only have been restored from the other runtime's serialized view
state. `__EVENTVALIDATION` is bound to the exact `__VIEWSTATE` string it was
issued with, which is why a payload has to travel whole.
[`ClientScriptManager.cs`](../../src/System.Web.ReferenceSource/UI/ClientScriptManager.cs#L1295)

Separately, the same page rendered on both runtimes is byte-identical once
`__VIEWSTATE`, `__VIEWSTATEGENERATOR`, and `__EVENTVALIDATION` are redacted.

## Why it works despite P48

`__VIEWSTATEGENERATOR` does not agree across runtimes and never will — measured
`CA0B0334` on Framework against `72DAA2F9` here for the same page. It does not
prevent interchange because it is not part of the key: on the `Framework45` path
`ObjectStateFormatter.GetSpecificPurposes` derives the key from the literal
strings `"TemplateSourceDirectory: ..."` and `"Type: ..."` upper-cased, both of
which agree when the page sits at the same application-relative path and
generates the same type name.
[`ObjectStateFormatter.cs`](../../src/System.Web.ReferenceSource/UI/ObjectStateFormatter.cs#L185)

The field is consulted on the **failure** path, where `VerifyClientStateIdentifier`
decides whether an invalid view state was meant for this page. A mixed farm is
the one deployment that makes that divergence reachable: an expired or corrupt
payload rendered on one runtime and posted to the other takes a different branch
than the same farm would take with matching nodes. The success path is
unaffected; the residual gap is in error reporting.

## What is not proven

- Forms authentication cookies, which ride the same key.
- Session state, which is in-process here and would need an out-of-process
  provider before a farm of any kind is coherent.
- Anything beyond a single page at the application root. The derived key depends
  on `TemplateSourceDirectory` and the generated type name agreeing, which was
  observed rather than reasoned about for one page.

## Consumer requirements

- An explicit literal `<machineKey>` on every node. Auto-generated keys are
  per-process here and per-machine on Framework (ledger P15), so a farm of any
  kind must not use them.
- No `,IsolateApps` or `,IsolateByAppId` suffix. It overwrites the first four
  bytes of even a literal key with a hash that does not agree across runtimes;
  see [machine key and ViewState bootstrap](machine-key-and-viewstate-bootstrap.md).
- `targetFramework` 4.5 or above, which P40 already requires, so the crypto
  provider is the portable managed path on both sides.

## Done when

- Resolved (2026-08-05, ADR 0045 policy): the reverse direction does not get a
  standing golden gate. The Framework-to-port direction stays gated by the
  captured payload; port-to-Framework acceptance is verified by an on-demand
  Windows procedure — render the page here, post the payload to a Framework
  node sharing the fixture's `<machineKey>` — to be re-run when anything in
  the derivation chain changes: `ObjectStateFormatter.GetSpecificPurposes`
  inputs, machine-key material handling, or `StringUtil` hashing. A future
  capture of a port-rendered payload replayed on Framework may promote this
  to a fixture if regressions ever demand it.
- The error-path divergence above is an accepted deviation, recorded in ledger
  P48: a corrupt or expired payload posted across runtimes takes this
  runtime's invalid-view-state branch rather than the branch a matching farm
  would take; the success path is unaffected, and the field is otherwise only
  compared against values this runtime produced.
