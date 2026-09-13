# IIS staticContent/clientCache

## Objective

Honor `system.webServer/staticContent/clientCache` the way IIS's native static
module did, measured in [readings CC1-CC15](../research/iis-client-cache-readings.md).
Today the element fails activation as an unsupported child of `<staticContent>`.

## Decisions

- All five attributes are honored: `cacheControlMode` (`NoControl`,
  `UseMaxAge`, `UseExpires`, `DisableCache`), `cacheControlMaxAge`,
  `httpExpires`, `cacheControlCustom`, `setEtag` (CC2-CC10).
- One `Cache-Control` header on every static answer the bridge produces, 200,
  `HEAD`, 206 and 304 alike, built from the caching profile's word, then
  `cacheControlCustom`, then `max-age=<seconds>` or `no-cache`, comma-joined
  with no space; nothing when every part is empty (CC1, CC4, CC8, CC11,
  CC13). `Expires` is sent verbatim under `UseExpires` alone (CC5-CC7).
  `setEtag="false"` drops the `ETag` and keeps `Last-Modified` (CC10).
- Managed answers are untouched (CC3, CC15).
- Root `web.config` only; the element in a `<location>` block or a folder
  `web.config` fails activation naming the file (CC12 measured per-path
  scoping; the first cut matches the other static sections).
- A span that does not parse, an unknown mode, or a CR/LF in `httpExpires` or
  `cacheControlCustom` fails activation naming the file and attribute, where
  IIS failed the static requests alone (CC14).
- The shipped baseline writes the schema defaults out:
  `<clientCache cacheControlMode="NoControl" cacheControlMaxAge="1.00:00:00" setEtag="true" />`.
- No reading or ledger IDs in code or config comments. No `Co-Authored-By`
  footers.

## Architecture

Runtime only. Nothing under `System.Web.ReferenceSource` changes.

`Compatibility/IisConfig/ClientCache.cs`, new:

```text
enum ClientCacheMode { NoControl, UseMaxAge, UseExpires, DisableCache }
record ClientCache(ClientCacheMode Mode, TimeSpan MaxAge, string HttpExpires,
    string Custom, bool SetEtag)
    string? CacheControl(string? profileWord)
    string? Expires
```

`ClientCacheSection.Apply` reads
`/configuration/system.webServer/staticContent/clientCache` in
`IisServerConfiguration.ApplyFile` for both files, baseline first, beside
`CustomHeaderSection`: the mode through the shared `IisCollectionReader.EnumValue`,
the span through `TimeSpan.TryParse` invariant, `setEtag` through
`OptionalBoolean`, the two strings verbatim after the CR/LF check. It refuses
`//location//clientCache`; `RefuseBelowTheRoot` joins the others in
`IisFolderHandlers.Load`. `IisCollectionReader.Apply` over `<staticContent>`
skips the `clientCache` element instead of refusing it: the schema gains the
names of sibling elements another reader owns. `IisServerConfiguration.ClientCache`
exposes the record.

`StaticFileBridgeHandler`, after `StaticFileHandler.ProcessRequestInternal`
returns: the block that writes the profile word becomes one call into the
record with that word. Null suppresses the default header as today; a value is
appended; `Expires` is appended when present; the `ETag` header is removed when
`SetEtag` is false. The 304 and 206 branches return into this block, which is
why one place covers them (CC11).

## Tests

Unit tests for what the port decides, one scenario for the wiring.

- `Runtime.Tests/Compatibility/IisConfig/ClientCacheTests`: the baseline's
  defaults load and answer no header; an application's `UseMaxAge` with a
  custom text and `setEtag="false"` loads; the header composition as one theory
  over the measured shapes (mode, custom, profile word → `Cache-Control`,
  `Expires`); the refusals as one theory (bad span, bad mode, CR in the
  custom text, `<location>`, folder file), each asserting the file and
  attribute. Two facts, two theories.
- Scenario on the `body-customerrors` shared host, which gains
  `<clientCache cacheControlMode="UseMaxAge" cacheControlMaxAge="00:01:00" cacheControlCustom="public" setEtag="false" />`
  inside a new `<staticContent>`. `ClientCacheOverKestrelTests`: `he/err404.json`
  served directly answers `Cache-Control: public,max-age=60`, no `ETag`, a
  `Last-Modified`; the same request with `If-Modified-Since` equal to that
  `Last-Modified` answers 304 with the same `Cache-Control`. One test. The
  host's other claims do not read static headers.

## Sequence

1. Runtime: record, reader, the collection-reader skip, baseline attribute,
   the bridge. Unit tests green.
2. Fixture amendment and the scenario. macOS suite green.
3. Docs: compatibility row `system.webServer/staticContent` names
   `clientCache` with the boundaries above; ledger P105; the fixtures README
   row for `body-customerrors`.
4. Windows and Linux rounds on the committed head.

## Done when

Every reading the port claims has a test or a named boundary, the three
rounds pass, and a `web.config` carrying a bad span, a bad mode or a sub-root
element stops the process at start-up naming the file and attribute.
