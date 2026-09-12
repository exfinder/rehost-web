# IIS custom response headers

## Objective

Honor `system.webServer/httpProtocol/customHeaders` the way IIS's protocol
module did, measured in [readings CH1-CH24](../research/iis-custom-headers-readings.md).
Today the section is tolerated and every row is silently ignored.

## Decisions

- Root `web.config` only, `add`, `remove` and `clear` in document order. A
  folder `web.config` or a `<location>` block carrying the section fails
  activation naming the file.
- Duplicate names, case-insensitively, fail activation (CH12, CH14). A name
  with characters outside the HTTP token set, or a value with CR or LF, fails
  activation too: IIS sent them raw (CH23) and Kestrel would fault the
  response instead, so the refusal moves to start-up and names the row.
- `X-Powered-By: ASP.NET` stays unsent. Ledger P68 already decided the host
  does not produce it, and the shipped baseline carries no `customHeaders`
  section, so there is no inherited row. `<remove name="X-Powered-By" />` is
  accepted and inert. Boundary: an application-level `add` of that name is
  sent here where IIS refused it as a duplicate (CH13).
- `<remove name="Server" />` is accepted and inert, as on IIS (CH19).
  `security/requestFiltering/@removeServerHeader="true"` turns Kestrel's own
  `Server` header off (CH20); `false` or absent leaves the host's setting
  alone.
- `Cache-Control` and `Content-Type` join an existing header with a comma;
  every other name becomes a second line (CH5-CH7). An empty value sends
  nothing (CH15).
- Values go out through the existing response-header encoding selector, so
  their bytes follow `<globalization responseHeaderEncoding>` like every other
  header. Boundary: IIS wrote configured values as ISO-8859-1 regardless
  (CH22); a non-ASCII value under the UTF-8 default differs by encoding.

## Architecture

### Layer 0: configuration (Runtime)

`Compatibility/IisConfig/CustomHeaders.cs`, new. One immutable record: the
ordered `(Name, Value)` rows after `add`/`remove`/`clear`, and
`RemoveServerHeader`.

- Read in `IisServerConfiguration.ApplyFile` for the application file only.
  The rows come through `IisCollectionReader.Apply` over an empty dictionary,
  which already gives `add`/`remove`/`clear` semantics and the duplicate-key
  refusal every other collection uses; the record keeps document order.
- `RemoveServerHeader` comes from `IisCollectionReader.OptionalBoolean` on
  `security/requestFiltering`.
- Refusals by XPath, each naming `configPath` and the element:
  `//location//customHeaders`, a row whose name is not a token or whose value
  carries CR or LF. `IisFolderHandlers.Load` refuses a folder file whose
  document has `system.webServer/httpProtocol/customHeaders`, beside its
  rewrite refusal.
- `IisServerConfiguration.CustomHeaders` exposes the record. An application
  with no section gets the empty record, not null.

### Layer 1: applying them (Hosting)

`CustomResponseHeaders.cs`, new, one static class:

```text
static void Register(HttpContext context, CustomHeaders headers)
    context.Response.OnStarting(() => Apply(context.Response.Headers, headers))
static void Apply(IHeaderDictionary target, CustomHeaders headers)
    for each row: skip an empty value; Cache-Control and Content-Type join
    the existing value with ","; every other name appends.
```

`RehostWebFormsMiddleware.InvokeAsync` calls `Register` first thing, before
the body-limit and root-escape answers, so every response the middleware
produces carries the rows: its own 413 and 403, the rewrite step's early
answers, the spooled System.Web response, and the WebSocket 101, which
Kestrel writes through the same response (CH2, CH3, CH24). The callback runs
once, at head-commit, after managed code has finished, which is why an
application's `Headers.Remove` and `ClearHeaders` cannot touch a configured
row (CH8).

`RehostWebFormsExtensions.AddRehostWebForms` sets
`KestrelServerOptions.AddServerHeader = false` when `RemoveServerHeader` is
true, inside the `Configure<KestrelServerOptions>` block that already exists.

Nothing under `System.Web.ReferenceSource` changes. `ResponseSpool` is not
touched: its `Append` per header and the `OnStarting` append meet in Kestrel's
dictionary, which writes one line per value.

## Tests

Unit:

- `Runtime.Tests/Compatibility/IisConfig/CustomHeadersTests`: add, remove,
  clear, document order, the duplicate and case-insensitive duplicate
  refusals, the `<location>` and folder refusals, the token and CR/LF
  refusals, `removeServerHeader` parsed, absent section gives the empty
  record. Each refusal asserts the file and element in the message.
- `Hosting.Tests/CustomResponseHeadersTests` over a `HeaderDictionary`: a
  second value for `X-Custom` is a second entry; `Cache-Control` and
  `Content-Type` join with a comma; an empty value adds nothing.

Scenario, on the `webserver` fixture's shared host, whose `X-Future-Tenant`
row becomes the honored row (renamed `X-Fixture: honored`) beside an added
`Cache-Control: no-store`; the fixture's "unhonored section tolerated" claim
moves to a `<urlCompression>` element. `CustomHeadersOverKestrelTests`:

- A page, a static file, the native 404 for a missing file, and the rewrite
  step's redirect (`/rw/old`) all carry `X-Fixture` (CH2, CH3).
- The page's `Cache-Control` reads `private,no-store` and the static file's
  `no-store` (CH6).
- A probe that appends `X-Fixture: app` gets two lines, the probe's first
  (CH5).
- A probe that removes `X-Fixture` still sends it (CH8).

`removeServerHeader` needs a host that sends `Server` at all; the scenario
host turns it off already, so that attribute is covered by the unit test and
the compatibility row, not a scenario.

## Sequence

1. Runtime: `CustomHeaders`, the `IisServerConfiguration` hook, the folder
   refusal. Unit tests green.
2. Hosting: `CustomResponseHeaders`, the middleware call, the Kestrel option.
   Unit tests green.
3. Fixture amendment and scenarios. macOS suite green.
4. Docs: compatibility row for `httpProtocol/customHeaders` to Partial with
   the boundaries above; ledger P101; the tenant row in the IIS configuration
   plan closed; the `webserver` row in the fixtures README.
5. Windows and Linux rounds on the committed head.

## Done when

Every reading the port claims has a test or a named boundary, the three
rounds pass, and a `web.config` whose section has a duplicate name, a bad
name or a sub-root placement stops the process at start-up naming the file
and the row.
