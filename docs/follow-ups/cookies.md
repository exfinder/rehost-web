# Cookies

Status: implemented on macOS `arm64`; Windows `x64` pending. Slice 4, split out
of [deferred request surfaces](deferred-request-surfaces.md). One open question
is recorded at the end.

## Scope

In:

- request `Cookie` header ingestion through `AspNetCoreWorkerRequest` into
  `HttpRequest.Cookies`, including sub-key values and the `$Path`/`$Domain`
  attribute forms;
- response `Set-Cookie` emission through the response spool and the ASP.NET Core
  commit: several cookies, attribute rendering (`domain`, `expires`, `path`,
  `secure`, `HttpOnly`, `SameSite`), and the classic-pipeline meaning of
  `Response.Cookies` mutation;
- `HttpCookie` semantics an application observes: sub-key collections,
  the defaults a cookie is born with, and `Shareable`;
- the `<httpCookies>` configuration defaults and the `appSettings` switches that
  change cookie behavior;
- request validation of cookie values.

Out:

- session state, forms authentication, anonymous identification, and roles —
  every cookie those subsystems own belongs to their own story;
- cookieless modes (`CookielessHelper`, `AppPathModifier`), which are reached
  from those same subsystems;
- output caching interaction (`Shareable`, `SuppressCachingCookiesIfNecessary`),
  which needs the caching slice;
- `Set-Cookie` written through `Response.Headers`/`AddHeader` rather than the
  cookie collection, which is the header story, not the cookie story;
- the integrated-pipeline incremental cookie path
  (`GenerateResponseHeadersForCookies`, `aspnet:AvoidDuplicatedSetCookie`,
  `HttpCookie.IsInResponseHeader`), unreachable here: this port is the classic
  pipeline ([ADR 0001](../adr/0001-classic-managed-pipeline.md)), where headers
  are generated once in `WriteHeaders`.

## What the managed path already provided

`HttpCookie`, `HttpCookieCollection`, `HttpCookiesSection`,
`HttpRequest.FillInCookiesCollection`, and the cookie block of
`HttpResponse.GenerateResponseHeaders` needed no porting. Every cookie path runs
over substrate the body and postback slices already exercise on both platforms
and reaches no platform-sensitive leaf, so their behavior is recorded below as
measurement rather than guarded by standing tests
([writing-tests](../writing-tests.md) rung 0). One imported-source change was
required and is described under the request-cookie defaults below.

## Port-owned seams

Two, both in the adapter, and each has one standing test in
`CookiesOverKestrelTests`:

- `AspNetCoreWorkerRequest.GetKnownRequestHeader(HeaderCookie)` — System.Web
  parses the header itself, so the adapter must hand it over whole
  (`Reads_The_Cookie_Header_Into_The_Request_Collection`).
- `ResponseSpool` plus the commit in `RehostWebFormsMiddleware` — the spool keeps
  each sent header as its own entry and the commit appends rather than assigns,
  so *n* cookies leave as *n* `Set-Cookie` lines
  (`Emits_One_Set_Cookie_Line_Per_Response_Cookie`), and an attribute-carrying
  line survives verbatim although it contains a comma
  (`Renders_Cookie_Attributes_As_Framework_Writes_Them`). Assigning instead of
  appending fails the first; truncating a value fails the second.

`Renders_Cookie_Attributes_As_Framework_Writes_Them` also depends on the shipped
`browserCaps` result substitution: `HttpCookie.SupportsHttpOnly` consults
`Request.Browser`, so a runtime where that answers nothing would drop `HttpOnly`
silently.

## Measured behavior

Over Kestrel, on this port:

| Sent or done | Observed |
| --- | --- |
| `Cookie: a=1; b=x&y=2; c` | three cookies; `b` has sub-keys `[null]=x`, `y=2`; `c` has an empty value |
| `Cookie: dup=1; dup=2` | both kept, as two entries under one name |
| Two `Cookie` header lines | joined with `", "` before parsing, so `a=1` and `b=2` become one cookie `a` with value `1, b=2` |
| `Cookie: $Path=/x` after a cookie | applied to the preceding cookie, not added |
| `new HttpCookie("plain", "value")` | `Set-Cookie: plain=value; path=/` |
| `Values["a"]="1"; Values["b"]="2"` | `Set-Cookie: multi=a=1&b=2; path=/` |
| every attribute set | `marked=value; domain=example.test; expires=Tue, 02-Jan-2035 03:04:05 GMT; path=/scoped; secure; HttpOnly; SameSite=Lax` |
| `Cookies.Add` then `Cookies.Set` for one name | one line, the second value |
| `Cookies.Add` then `Cookies.Remove` | no line at all — not an expiry |
| any response cookie | also appears in the same request's `Request.Cookies` |
| `Cookie: evil=<script>…` | 500, `HttpRequestValidationException`, "A potentially dangerous Request.Cookies value was detected from the client" |

`Set` and `Remove` mean what they do because headers are generated once, in
`WriteHeaders`. Deleting a cookie in a browser still requires the usual expired
cookie; removing it from the collection only stops this response from setting it.

The joined duplicate `Cookie` header is the HTTP rule for repeated headers, which
is also what IIS hands `GetKnownRequestHeader`; RFC 6265 forbids a client from
sending more than one. It has not been verified against a real IIS.

## Framework readings (2026-08-05, `win-oracle`, `System.Web` 4.8.9319.0)

Taken by read-only reflection over the GAC assembly:

- `httpCookies` appears in exactly one file under the Framework configuration
  directory, `machine.config`, and only as the section declaration. No config
  file on the machine carries a `<httpCookies>` element, and neither does the
  Visual Studio 4.8.1 Web Forms template beside this checkout, so a newly created
  application runs on the section defaults — which read
  `httpOnlyCookies=False`, `requireSSL=False`, `domain=""`,
  `sameSite=Unspecified`. The port's shipped root configuration omits the element
  for the same reason and lands on the same defaults.
- `AppSettings.SuppressSameSiteNone` and `AvoidDuplicatedSetCookie` are both
  `False`, matching this port's constants.
- `HttpRequest.CreateCookieFromString` takes `(String s, Boolean
  useConfiguredDefaults)`, a parameter the pinned Reference Source does not have.
  `AppSettings.FixCookieDefaults` reads `True`, and its key is
  `aspnet:EnsureCookieDefaults`. Reading the IL, `FillInCookiesCollection` passes
  that switch and `HttpCookie.TryParse` passes a literal `false`.

## The request-cookie defaults (imported-source change)

The one behavioral gap this slice found and closed. On 4.8.1 a cookie parsed
from the request header is born with the `<httpCookies>` defaults applied; on the
pinned Reference Source it is born with the field defaults, whose `SameSite` is
`None` (0) rather than `Unspecified` (-1).

It is observable on one path, and that path is a common idiom: an application
that re-issues the cookie it received —
`Response.Cookies.Add(Request.Cookies["a"])` — emitted
`a=1; path=/; SameSite=None` here against `a=1; path=/` on Framework. `SameSite=None`
without `Secure` is rejected outright by current browsers, so the port's version
of the cookie would be dropped by the client. With a non-default `<httpCookies>`
the same gap loses `domain`, `secure`, and `httpOnly` as well.

`PROJECT.md` ranks the behavior of a 4.8.1 runtime above the pinned Reference
Source, so the port follows 4.8.1: `CreateCookieFromString` takes the parameter,
`FillInCookiesCollection` passes `AppSettings.FixCookieDefaults`, `TryParse`
keeps passing false, `SetDefaultsFromConfig` becomes internal, and
`aspnet:EnsureCookieDefaults` defaults to true and can be turned off, exactly as
on Framework. `Re_Issuing_A_Received_Cookie_Adds_No_Same_Site_Attribute` is the
guard; reverting the call site fails it alone.

This is the first place the port has had to follow shipped 4.8.1 against the
pinned snapshot, which stopped receiving servicing fixes. It will not be the
last, and there is no mechanism that finds the others.

## Open: non-ASCII response header values

A cookie whose value carries non-ASCII text — `café` — fails the request with an
empty 500. Kestrel validates response header values as ASCII unless the host sets
`KestrelServerOptions.ResponseHeaderEncodingSelector`, and
`HttpHeaders.ValidateHeaderValueCharacters` throws
`InvalidOperationException: Invalid non-ASCII or control character in header:
0x00E9` inside the middleware's commit. Framework writes header bytes in
`Response.HeaderEncoding`, which defaults to UTF-8 and is configurable through
`<globalization responseHeaderEncoding>`, so it sends the cookie.

This is not cookie-specific — any header value the application sets behaves the
same way — and the remedies differ in who owns them:

- have `AddRehostWebForms` configure the Kestrel selector, which restores
  Framework's behavior but mutates a server-wide option on behalf of the host,
  including endpoints System.Web never sees;
- leave it to the host and document the option, which keeps ownership clean and
  leaves a failure with no diagnostic;
- reject a non-ASCII header value in the adapter with a message naming the header
  and the option, which is honest but is still a failed request.

Deliberately unresolved here, and deliberately untested: a standing test would
pin a defect as a contract. See
[the host adapter](aspnet-core-host-adapter.md).

## Traps for whoever tests near this

- `ScenarioClient` keeps its cookie container off on purpose. Assert on
  `SetCookies`, never on `Headers["Set-Cookie"]`: repeated header values are
  joined with `", "` there, and an `expires` attribute contains a comma of its
  own, so the joined form cannot be split back.
- `HttpClient` merges repeated `Cookie` request headers into one line with
  `"; "` before they reach the wire, so a test cannot reach the adapter's
  duplicate-header join through it; that measurement needs `curl` or a socket.
- `Expires` must be `DateTimeKind.Utc` in a test that pins the rendered line:
  `FormatHttpCookieDateTime` calls `ToUniversalTime`, so an unspecified kind
  renders the host's time zone.
- Reading `Request.Cookies` before the handler adds response cookies is safe:
  adding one calls `Request.ResetCookies`, so a later read still sees it.
