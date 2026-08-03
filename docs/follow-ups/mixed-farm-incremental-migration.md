# Mixed farm and incremental migration

Status: observed on macOS `arm64` against a .NET Framework 4.8.1 oracle, not
gated. Priority: high — this is a deployment capability future consumers depend
on, and it is currently evidence rather than a standing test.

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

Content crosses too, not merely the signature. The fixture's `Page_Load` assigns
its label only when `!IsPostBack`, so the value present after a cross-runtime
postback can only have been restored from the other runtime's serialized view
state.

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

- **`__EVENTVALIDATION` interchange.** The cross-post deliberately sent a bare
  payload with an empty `__EVENTTARGET`, to keep `ValidateEvent` off the path
  while MAC behavior was isolated. A real postback carrying a button also carries
  this field. It is bound to the exact `__VIEWSTATE` string it was issued with,
  so carrying both from one render is the case to test.
  [`ClientScriptManager.cs`](../../src/System.Web.ReferenceSource/UI/ClientScriptManager.cs#L1295)
  Until this passes, the proven claim is "view state interchanges", not "a mixed
  farm serves real postbacks".
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

- A committed fixture holds a Framework-captured payload with all three fields
  and a control value, and a test asserts the port raises the control's event.
- The reverse direction runs from the oracle prototype rather than from
  throwaway scaffolding, so it survives as a regression gate.
- The error-path divergence above is either covered by a differential or
  recorded as an accepted deviation with its consumer-visible shape stated.
