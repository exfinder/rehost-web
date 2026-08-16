# ASP.NET Core host adapter residuals

Evidence behind the open items on
[the adapter follow-up](../follow-ups/aspnet-core-host-adapter.md). The
boundary itself is [ADR 0003](../adr/0003-host-boundary.md); prior adapter work
was decided by taking an IIS reading on a Windows host and then cutting the
narrowest seam (ledger P59, P72). Nothing here is a decision — the last section
lists what needs deciding. Outcome (2026-08-16): decisions 1–7 and 9–11 landed
as ledger P76–P78 (client certificates and streaming recorded, WebSockets a
high-priority follow-up); the [Framework readings](#framework-readings) at the
end are the evidence.

Line references are `file:line` against the tree at the time of writing.
Statements marked **inference** are reasoning from source, not a reading.

Cast of files:

| role | file |
|---|---|
| adapter | `src/Rehost.WebForms.Hosting/AspNetCoreWorkerRequest.cs` |
| commit | `src/Rehost.WebForms.Hosting/RehostWebFormsMiddleware.cs`, `ResponseSpool.cs` |
| registration | `src/Rehost.WebForms.Hosting/RehostWebFormsExtensions.cs` |
| contract | `src/System.Web.ReferenceSource/WorkerRequest.cs` (`HttpWorkerRequest`) |
| Framework reference impls | `src/System.Web.ReferenceSource/Hosting/IIS7WorkerRequest.cs`, `Hosting/ISAPIWorkerRequest.cs` |
| consumers | `src/System.Web.ReferenceSource/HttpRequest.cs`, `HttpResponse.cs`, `HttpServerVarsCollection.cs`, `HttpClientCertificate.cs`, `HttpWriter.cs` |

## Result in one paragraph

The server-variable surface is in better shape than the follow-up implies —
every variable a Web Forms application typically reads is either right or
IIS-only — but three worker-request members the adapter **does not override**
put wrong values into variables and properties that applications do read:
`IsSecure()` is permanently `false` (so `Request.IsSecureConnection` is false
over HTTPS and `SERVER_PORT_SECURE` is `0`), and `GetServerName()` falls back
to the connection's local IP address, which is what `Request.Url` is built
from. Separately, `HttpServerVarsCollection` never consults the worker request
for a name outside its hardcoded list when the worker request is not an
`IIS7WorkerRequest`, so adding arms to the adapter's `GetServerVariable` cannot
by itself make `Request.ServerVariables["ANYTHING_ELSE"]` answer. Response
header encoding is genuinely unimplemented (the `SetHeaderEncoding` callback is
a base no-op) and Kestrel's per-name, server-wide selector means the fix is a
byte-transparent Latin-1 commit rather than a pass-through. There *is* a real
response spill — host-owned, 32 KiB, `FileBufferingWriteStream` — and no test
touches it; every "spill" test in the tree is request-body spill. Client
certificates, WebSockets, and compression each already fail or degrade
predictably; the open question for each is whether to translate Kestrel's
equivalent or write the boundary down.

---

## 1. Server variables

### 1.1 How Framework fills the collection

`HttpRequest.FillInServerVariablesCollection` (`HttpRequest.cs:554-641`) is the
whole authority. It adds, in order:

* two computed aggregates, `ALL_HTTP` and `ALL_RAW`, from
  `CombineAllHeaders` (`HttpRequest.cs:455-501`), which walks
  `GetKnownRequestHeader(0..39)` and `GetUnknownRequestHeaders()`;
* **dynamic** entries whose value is recomputed per read by
  `CalcDynamicServerVariable` (`HttpRequest.cs:504-535`) — `AUTH_TYPE`,
  `AUTH_USER`/`REMOTE_USER`, `PATH_INFO`, `PATH_TRANSLATED`, `QUERY_STRING`,
  `SCRIPT_NAME`/`URL`;
* **static** entries taken from a named `HttpRequest`/`HttpWorkerRequest`
  member;
* **static** entries taken from `_wr.GetServerVariable(name)` verbatim
  (`HttpRequest.cs:549-552`) — this overload does *not* map `null` to `""`, so
  a variable the worker request declines stays `null` in the collection;
* every request header as `HTTP_*`, known headers via
  `GetServerVariableNameFromKnownRequestHeaderIndex` and unknown ones via
  `ServerVariableNameFromHeader` (uppercase, `-` → `_`)
  (`HttpRequest.cs:451-453, 629-640`).

Two structural facts matter more than any individual variable:

1. **Names outside that list are unreachable off IIS.**
   `HttpServerVarsCollection.Get` (`HttpServerVarsCollection.cs:186-208`) only
   falls through to `_request.FetchServerVariable(name)` → `_wr.GetServerVariable`
   when `_iis7workerRequest != null`. On this host that field is null, so the
   collection answers `null` for `UNENCODED_URL`, `HTTP_URL`, `HTTP_VERSION`,
   `CACHE_URL`, `APP_POOL_ID`, `SCRIPT_TRANSLATED` and every native-module
   variable **no matter what the adapter's `GetServerVariable` returns**.
   Framework behaved the same way off IIS7 (classic ISAPI went through
   `ISAPIWorkerRequestInProc.GetServerVariable` → `GetServerVariableCore`, but
   only for names the *collection* already carried).
2. **`ServerVariables.Set` is a hard failure off IIS7** —
   `HttpServerVarsCollection.cs:216-219` throws `PlatformNotSupportedException`.
   Rewrite-style modules that publish variables cannot work on this host, and
   they fail with Framework's own exception. `HttpRequest.SetDynamicCompression`
   (`HttpRequest.cs:2341-2357`) and `AppendToLogQueryString`
   (`HttpRequest.cs:2359+`) already guard with `as IIS7WorkerRequest` and
   return silently.

The two sample apps confirm the collection itself is not hot: no
`Request.ServerVariables[...]` appears in `apps/WebFormsApplication`,
`apps/WebFormsIdentityApplication`, or the sibling `../WebFormsApplication`.
What they *do* use is the property surface built on these members:
`Request.RawUrl` (`ViewSwitcher.ascx.cs:39` in both apps),
`Request.IsSecureConnection` (`WebFormsIdentityApplication/Site.Master.cs:41`),
and `Context.User.Identity` (`Site.Master.cs:57,63`,
`Account/OpenAuthProviders.ascx.cs:26-28`). Inside the runtime the collection is
read by `HttpClientCertificate` (§4.1), `UI/ViewStateException.cs:68-72`
(`REMOTE_ADDR`, `REMOTE_PORT`, `HTTP_USER_AGENT`, `HTTP_REFERER`, `PATH_INFO`),
browser-capability evaluation (`Configuration/CapabilitiesState.cs:94`,
`Configuration/HttpCapabilitiesEvaluator.cs:333`), and request validation
(`UnvalidatedRequestValues.cs:131`).

### 1.2 Variable-by-variable

Port column: what `Request.ServerVariables[name]` yields today, which is not
always what `AspNetCoreWorkerRequest.GetServerVariable`
(`AspNetCoreWorkerRequest.cs:191-212`) would answer — the collection reaches the
adapter only through the `AddServerVariableToCollection(name)` overload.

| variable | Framework source | port today | class |
|---|---|---|---|
| `ALL_HTTP` | `CombineAllHeaders(false)`, `HttpRequest.cs:455-501,560` | same code, headers from Kestrel | matches, modulo §1.3 |
| `ALL_RAW` | `CombineAllHeaders(true)`, `:561`. On IIS7 the *real* `ALL_RAW` is also where known/unknown headers come from (`IIS7WorkerRequest.cs:1240-1250`) | rebuilt from Kestrel's parsed headers, not received bytes | differs (order, spelling, folding) |
| `APPL_MD_PATH` | worker request. Classic answered `HttpRuntime.AppDomainAppId` (`ISAPIWorkerRequest.cs:2861`); IIS7 the metabase path (`/LM/W3SVC/1/ROOT`) | `null` (`AspNetCoreWorkerRequest.cs:208-210` default) | IIS-only |
| `APPL_PHYSICAL_PATH` | `_wr.GetAppPathTranslated()`, `:565` | physical root + separator (`AspNetCoreWorkerRequest.cs:110-113`) | matches |
| `AUTH_TYPE` | dynamic, `User.Identity.AuthenticationType` or `""`, `:508-513` | same (`Context.User` is portable) | matches |
| `AUTH_USER`, `REMOTE_USER` | dynamic, `User.Identity.Name` or `""`, `:514-519,573` | same | matches |
| `AUTH_PASSWORD` | worker request (IIS Basic auth) | `null` | IIS-only |
| `LOGON_USER` | worker request (Windows auth) | `null` | IIS-only; Windows auth is already Unassessed (`compatibility.md:138`) |
| `CERT_COOKIE`, `CERT_FLAGS`, `CERT_ISSUER`, `CERT_KEYSIZE`, `CERT_SECRETKEYSIZE`, `CERT_SERIALNUMBER`, `CERT_SERVER_ISSUER`, `CERT_SERVER_SUBJECT`, `CERT_SUBJECT` | worker request, `:575-583` | `null` | empty default → §4.1 |
| `CONTENT_LENGTH` | `GetKnownRequestHeader(HeaderContentLength)` else `"0"`, `:585-586` | same | matches |
| `CONTENT_TYPE` | `this.ContentType`, `:588` | same | matches |
| `GATEWAY_INTERFACE` | worker request; IIS answered `CGI/1.1` | `null` | IIS-only, trivially translatable |
| `HTTPS` | worker request, `:592` | `"on"`/`"off"` from `Request.IsHttps` (`AspNetCoreWorkerRequest.cs:207`) | matches |
| `HTTPS_KEYSIZE`, `HTTPS_SECRETKEYSIZE`, `HTTPS_SERVER_ISSUER`, `HTTPS_SERVER_SUBJECT` | worker request (Schannel), `:593-596` | `null` | IIS-only; a TLS feature could supply them |
| `INSTANCE_ID`, `INSTANCE_META_PATH` | worker request, `:598-599` | `null` | IIS-only |
| `LOCAL_ADDR` | `_wr.GetLocalAddress()`, `:601` | connection local IP (`AspNetCoreWorkerRequest.cs:95-98`) | matches |
| `PATH_INFO` | dynamic → `this.Path` — the **whole** URL path, not the path-info tail (`:520-522,603`) | same, so correct | matches, but see §1.4 |
| `PATH_TRANSLATED` | dynamic → `PhysicalPathInternal`, `:523-525,604` | same, via adapter `MapPath` | matches |
| `QUERY_STRING` | dynamic → `QueryStringText`, `:526-528,606` | same | matches |
| `REMOTE_ADDR` | `this.UserHostAddress` → `GetRemoteAddress()`, `:608, 1838-1846` | connection remote IP | matches |
| `REMOTE_HOST` | `this.UserHostName` → `GetRemoteName()`, base falls back to the address (`WorkerRequest.cs:532-535`), `:609` | adapter does not override `GetRemoteName` → address | matches IIS default (reverse DNS off) |
| `REMOTE_PORT` | worker request, `:611` | connection remote port (`AspNetCoreWorkerRequest.cs:204-205`); exercised by `tests/Rehost.WebForms.ScenarioProbes/RequestBodyHandlers.cs:129` | matches |
| `REQUEST_METHOD` | `this.HttpMethod`, `:613` | same | matches |
| `SCRIPT_NAME`, `URL` | dynamic → `Request.FilePath`, `:615,625` | same (P72 split, `Compatibility/Hosting/RequestPathInfo.cs`) | matches |
| `SERVER_NAME` | `_wr.GetServerName()`, `:617`. IIS derived it from the `Host` header | **adapter does not override `GetServerName`** → base returns `GetLocalAddress()` (`WorkerRequest.cs:541-544`) → the connection's local IP | **differs** |
| `SERVER_PORT` | `_wr.GetLocalPortAsString()` → `GetLocalPort()`, `:618`, `WorkerRequest.cs:460-462` | connection local port | matches for a direct listener; differs behind a proxy |
| `SERVER_PORT_SECURE` | `_wr.IsSecure() ? "1" : "0"`, `:620` | **adapter does not override `IsSecure`** → base `false` (`WorkerRequest.cs:625-628`) → always `"0"` | **differs** |
| `SERVER_PROTOCOL` | `_wr.GetHttpVersion()`, `:622` | `Request.Protocol` → `"HTTP/1.1"`, `"HTTP/2"`, `"HTTP/3"` (`AspNetCoreWorkerRequest.cs:80-83`) | matches on HTTP/1.1; unknown for h2/h3 (§4.4) |
| `SERVER_SOFTWARE` | worker request; IIS answered `Microsoft-IIS/10.0` | `null` | IIS-only |
| `HTTP_*` (40 known + all unknown) | `GetKnownRequestHeader` / `GetUnknownRequestHeaders`, `:629-640`; name table `WorkerRequest.cs:1411-1455` | same, from Kestrel's collection | matches, modulo §1.3 |

Not in Framework's collection at all, so `null` on this host regardless of the
adapter: `HTTP_URL`, `HTTP_VERSION`, `UNENCODED_URL`, `CACHE_URL`,
`APP_POOL_ID`, `SCRIPT_TRANSLATED`, `WEBSOCKET_VERSION`, `IIS_WEBSOCK`
(`IIS7WorkerRequest.cs:2590,2595`), `IIS_EnableDynamicCompression`,
`IS_LOGIN_PAGE`, `LOG_QUERY_STRING` (`HttpRequest.cs:2368`). On IIS these were
reachable only through `HttpWorkerRequest.GetServerVariable` directly or through
the IIS7 fall-through at `HttpServerVarsCollection.cs:196-203`. `RAW_URL` is not
an IIS variable; the equivalent is `Request.RawUrl`, settled by P72.

### 1.3 Header-derived values: two known deltas

* **Repeated headers.** The adapter joins repeated values with `", "`
  (`AspNetCoreWorkerRequest.cs:351-354`, asserted by
  `tests/Rehost.WebForms.Hosting.Tests/AspNetCoreWorkerRequestTests.cs:69-76`).
  IIS's `ALL_RAW` is the received block, so two `X-R:` lines appeared as two
  lines there while `GetUnknownRequestHeader` returned IIS's own folding.
  Whether IIS joined with `", "` or `","` is a reading, not a source fact —
  **inference** that `", "` is right.
* **Underscores.** The adapter's `HTTP_` fallback maps back with
  `name.Substring(5).Replace('_', '-')` (`AspNetCoreWorkerRequest.cs:208-210`),
  so a header literally named `X_Custom` is unreachable as `HTTP_X_CUSTOM`.
  Framework's collection is unaffected because it builds `HTTP_*` names *from*
  the headers (`HttpRequest.cs:451-453`) rather than parsing them back. The
  adapter arm therefore only matters for direct `GetServerVariable` callers, of
  which there are three in-tree (`HttpDebugHandler.cs:182-183`,
  `Security/WindowsAuthenticationModule.cs:138-139`,
  `State/sqlstateclientmanager.cs:324`) and all read `LOGON_USER`/`AUTH_TYPE`.

### 1.4 The adapter's `GetServerVariable` disagrees with its own members

Three arms of `AspNetCoreWorkerRequest.GetServerVariable` answer differently
from the member the collection actually uses:

| name | `GetServerVariable` arm | collection uses | disagreement |
|---|---|---|---|
| `SERVER_NAME` | `Request.Host.Host` (`:195`) | `GetServerName()` → local IP | host header vs IP |
| `SERVER_PORT` | `Host.Port ?? (IsHttps ? 443 : 80)` (`:196,364-367`) | `GetLocalPort()` | host header port vs socket port |
| `SERVER_PORT_SECURE` | `IsHttps ? "1" : "0"` (`:197`) | `IsSecure()` → `false` | never agrees over HTTPS |
| `PATH_INFO` | `GetPathInfo()` — the tail (`:202`) | dynamic `Request.Path` — the whole path | different meaning |

The arms that agree (`REQUEST_METHOD`, `QUERY_STRING`, `SCRIPT_NAME`,
`APPL_PHYSICAL_PATH`, `REMOTE_*`, `LOCAL_ADDR`, `HTTPS`, `SERVER_PROTOCOL`) are
mostly dead code for the same reason: the collection reads them from members.
`REMOTE_PORT`, `HTTPS`, and `APPL_MD_PATH` are the arms the collection genuinely
consults (they use the `AddServerVariableToCollection(name)` overload).

### 1.5 Blast radius of the two unoverridden members

`IsSecure()` and `GetServerName()` are not just server variables:

* `Request.IsSecureConnection` is `_wr.IsSecure()` (`HttpRequest.cs:1393-1399`)
  → always `false`. The Identity template reads it
  (`apps/WebFormsIdentityApplication/.../Site.Master.cs:41`), and
  `FormsAuthentication`/cookie `Secure` decisions elsewhere key on the same
  idea.
* `GetProtocol()` is `IsSecure() ? "https" : "http"`
  (`WorkerRequest.cs:633-635`) → always `"http"`.
* `Request.Url` and `UrlInternal` are built as
  `GetProtocol() + "://" + GetServerName() [+ ":" + GetLocalPortAsString()] + Path + query`
  (`HttpRequest.cs:1900-1983`), because `AppSettings.UseHostHeaderForRequestUrl`
  defaults to `false` (`Util/AppSettings.cs:38-39`). On this host that yields
  `http://127.0.0.1:5000/x`; on IIS it yielded the `Host` header's authority
  and the real scheme.

No test in the tree asserts `Request.Url.Host`, `Request.Url.Scheme`, or
`Request.IsSecureConnection` — the P72 probe prints only
`request.Url.AbsolutePath` and `request.Url.Query`
(`tests/parity/src/Rehost.WebForms.Parity.Probes/PathProbeHandler.cs:24,28`),
which is why the readings did not catch it.

---

## 2. Header encoding

### 2.1 Response headers on Framework

`<globalization responseHeaderEncoding>` defaults to `utf-8`
(`Configuration/GlobalizationSection.cs:53-54,131-146`). `HttpResponse.HeaderEncoding`
reads it from the last-known-good config, refuses `Encoding.Unicode`, and
refuses to change after headers are written
(`HttpResponse.cs:1868-1898`). Once per header block, `HttpResponse` pushes it
into the worker request before sending any header:

```
_wr.SendStatus(this.StatusCode, this.StatusDescription);
Debug.Assert(!this.HeaderEncoding.Equals(Encoding.Unicode));
_wr.SetHeaderEncoding(this.HeaderEncoding);
```
(`HttpResponse.cs:626-629`; `HttpHeaderCollection.cs:178` does the same for the
IIS7 collection.)

`IIS7WorkerRequest` stores it (`:46` default `Encoding.UTF8`, set at `:802-804`)
and encodes **both the name and the value** with it into null-terminated byte
arrays handed to native IIS (`:2050-2077`, via
`StringUtil.GetNullTerminatedByteArray`, `Util/StringUtil.cs:332-342`).
`ISAPIWorkerRequest` appends header text to a `StringBuilder`
(`:1292-1323`) and encodes the whole block once at send with the same field
(`:801`, `_headers.GetEncodedBytesBuffer(_headerEncoding)`). So on Framework a
header value containing `é` leaves as UTF-8 `C3 A9` by default and as `E9` with
`responseHeaderEncoding="iso-8859-1"`.

### 2.2 Response headers on this host

`SetHeaderEncoding` is `internal virtual` with an empty body
(`WorkerRequest.cs:935-937`) and the adapter does not override it. Header
name/value pairs go into the spool as strings
(`AspNetCoreWorkerRequest.cs:271-279`, `ResponseSpool.cs:58-62`) and the commit
appends them to `response.Headers` (`RehostWebFormsMiddleware.cs:82-85`).
Kestrel does the encoding.

Kestrel's default (verified against `dotnet/aspnetcore` `release/10.0`, which
matches the installed `Microsoft.AspNetCore.App 10.0.10`):
`KestrelServerOptions.ResponseHeaderEncodingSelector` defaults to
`DefaultHeaderEncodingSelector = _ => null`, documented as "or `null` to use the
default `ASCIIEncoding`", and `HttpHeaders.ValidateHeaderValueCharacters` sets
`requireAscii` when the selector is the default or returns null, then throws
`InvalidOperationException` on the first non-ASCII character. The port therefore
pins the selector to UTF-8 for every header name
(`RehostWebFormsExtensions.cs:31-32`), which reproduces Framework's *default*
and nothing else.

Net: `Response.HeaderEncoding` and `<globalization responseHeaderEncoding>` are
inert on this host — the compatibility map already says so
(`docs/compatibility.md:80`).

**The seam is awkward and worth naming.** Kestrel's selector is keyed by header
*name* and lives on server options, i.e. it is process-wide and cannot vary per
request, while Framework's encoding is per response. The only faithful
translation is byte-transparent: set `ResponseHeaderEncodingSelector` to
`Encoding.Latin1` for all names (Latin-1 writes each char's low byte verbatim),
have the adapter override `SetHeaderEncoding` to record the response's encoding
on the spool, and have the commit hand Kestrel
`Encoding.Latin1.GetString(responseEncoding.GetBytes(value))`. That reproduces
UTF-8 by default and any configured encoding exactly, at the cost of one extra
string per header. **Inference**, not measured.

### 2.3 Request headers

Kestrel's default request decoding (`RequestHeaderEncodingSelector` → null,
documented as "the default `UTF8Encoding`") resolves to
`StringUtilities.GetAsciiOrUTF8String`: ASCII fast path, then strict UTF-8, and
an `InvalidOperationException` on invalid UTF-8, which surfaces as 400. A
`RequestHeaderEncodingSelector` returning `Encoding.Latin1` restores byte
transparency.

Framework has **no** `requestHeaderEncoding` setting. What it did instead:

* Classic ISAPI decoded the basics block and the all-server-variables block with
  `Encoding.Default` — the process ANSI code page
  (`Hosting/ISAPIWorkerRequest.cs:704, 2851`) — and parsed headers out of
  `ALL_RAW` (`:715-725`).
* IIS7 integrated fetches `HTTP_*` server variables through
  `MgdGetServerVariableA` and decodes with `Marshal.PtrToStringAnsi`
  (`Hosting/IIS7WorkerRequest.cs:766-773, 1150-1166`;
  `Util/StringUtil.cs:115-117`) — again the ANSI code page. But
  `GetKnownRequestHeader`/`GetUnknownRequestHeaders` parse `ALL_RAW` obtained
  through the **wide** entry point (`:1168-1184, 1240-1250`), so the byte →
  UTF-16 conversion happened inside native IIS and its code page is **not
  visible in managed source**.

So the exact question — does IIS widen request header bytes with `CP_ACP`
(1252 on a US machine) or Latin-1, and does `Request.Headers["X"]` agree with
`ServerVariables["HTTP_X"]` — can only be answered by a reading (§5, R5). Note
the two paths can legitimately disagree on IIS7: one is ANSI, the other is
whatever the wide native call produced.

---

## 3. Response spill

### 3.1 There is a response spill, and it is host-owned

Runtime side, the port never spills. `HttpWriter.CreateNewMemoryBufferElement`
selects `HttpResponseManagedBufferElement` off Framework
(`HttpWriter.cs:994-1000`; the pooled `HttpResponseUnmanagedBufferElement` at
`:266-313` is `#if NETFRAMEWORK` and native-pool bound), and
`HttpFileResponseElement` (`:496-612`) only carries a *reference* — its `Send`
calls `wr.SendResponseFromFile(name, offset, size)` (`:601-609`). So the runtime
holds the whole managed body in RAM, exactly as Framework did.

Host side, `ResponseSpool.Write` opens a `FileBufferingWriteStream` with a
32 KiB memory threshold and no buffer limit
(`ResponseSpool.cs:20, 70-85`), created under
`HttpRuntime.CodegenDir ?? Path.GetTempPath()`
(`ClassicPipelineActivation.cs:36-39`). Above 32 KiB in a single contiguous run
of `SendResponseFromMemory`, the body lands on disk. `WriteFile` resets the
current run (`:92`), so a memory / file / memory sequence produces two
independent buffering streams. At commit each segment is either
`response.SendFileAsync` or `DrainBufferAsync` (`:119-131`).

Cleanup: `FileBufferingWriteStream` creates its temp file with
`FileOptions.DeleteOnClose` and `DrainBufferAsync` disposes the file stream
after copying, so the file goes away on drain *and* on dispose.
`ResponseSpool.Dispose` disposes every buffering segment (`:134-140`) and the
middleware's `using var workerRequest` (`RehostWebFormsMiddleware.cs:40-44`)
covers the success, exception, and abort paths.

### 3.2 What "response-spill exercise" concretely needs

No test touches it. Grepping `spill` across the tree returns only request-body
work: `RequestBodyOverKestrelTests.cs:51-58` (`?mode=spill`),
`RawRequestSaveOverKestrelTests.cs`, `UploadSaveOverKestrelTests.cs`, the
`X-Spilled` probe header (`ScenarioProbes/RequestBodyHandlers.cs:58-60`,
`SaveHandler.cs:21`), and the `body` fixture note
(`tests/Rehost.WebForms.ScenarioHost/fixtures/README.md:14`). Grepping
`ResponseSpool|DefaultMemoryThreshold` finds no test at all; the spool
assertions in `AspNetCoreWorkerRequestTests.cs:548-700` all use tiny writes and
never cross the threshold.

To satisfy the follow-up's "cannot pass without exercising disk":

1. A Kestrel scenario page emitting well over 32 KiB, asserting the body is
   byte-exact end to end (a spilled response is where an off-by-one in
   `DrainBufferAsync` ordering would show).
2. The same with a file segment before and after the large memory run, so the
   two-buffering-stream shape is covered.
3. Temp-directory census before and after: same file set on success, on client
   disconnect mid-request, and on a commit failure (an unwritable
   `response.Body`, or the existing "file shrank" failure at
   `ResponseSpool.cs:109-117,142-157`).
4. `ResponseSpool`'s constructor already takes a threshold (`:30-37`) but
   `AspNetCoreWorkerRequest` always passes the default (`:41`). Either the
   scenario writes a genuinely large body, or the threshold becomes an
   adapter-level option so a unit test can drive it at, say, 64 bytes. The
   former is the honest test; the latter makes the cleanup matrix cheap.

---

## 4. Certificates, compression, upgrades, protocols, streaming

### 4.1 Client certificates

Members: `GetClientCertificate`, `GetClientCertificateValidFrom`,
`GetClientCertificateValidUntil`, `GetClientCertificateBinaryIssuer`,
`GetClientCertificateEncoding`, `GetClientCertificatePublicKey`
(`WorkerRequest.cs:1117-1160`; defaults `new byte[0]`, `DateTime.Now`, `0`).
IIS7 implements all six from one native fetch
(`IIS7WorkerRequest.cs:2430-2470+`); classic ISAPI at `:1540-1570`.

The adapter overrides none. But the base defaults are never even read, because
`HttpClientCertificate`'s constructor reads `CERT_FLAGS` first, and returns
early when bit 0 is clear (`HttpClientCertificate.cs:129-137`). `CERT_FLAGS` is
`null` on this host, so `Request.ClientCertificate.IsPresent` is `false` and
every other member keeps its field default. That is a *coherent* "no client
certificate" — the same thing IIS reported without certificate negotiation — so
today's behavior is a silent, correct-shaped empty rather than a wrong value.

The gap is that Kestrel *can* have one:
`context.Features.Get<ITlsConnectionFeature>()?.ClientCertificate` (or
`context.Connection.ClientCertificate`). Translating it means the adapter
answering `CERT_FLAGS` (`1` present, `| 2` invalid — `IsValid` is
`(_Flags & 0x2) == 0`, `HttpClientCertificate.cs:120-125`), `CERT_SUBJECT`,
`CERT_ISSUER`, `CERT_SERIALNUMBER`, `CERT_COOKIE`, `CERT_KEYSIZE`,
`CERT_SECRETKEYSIZE`, plus `RawData` / `NotBefore` / `NotAfter` /
`IssuerName.RawData` / `PublicKey` / `ContentType` for the six members.

Testability: the adapter side is a **unit test** with a fake `HttpContext`
carrying an `ITlsConnectionFeature`. End-to-end needs a **Kestrel scenario**
with a self-signed client certificate. The exact string formats of `CERT_*`
need an **IIS reading** (R6) — everything else is decidable from source.

### 4.2 Compression

No `HttpWorkerRequest` member is involved. IIS compressed natively, outside the
managed pipeline; the only managed touchpoint is
`HttpRequest.SetDynamicCompression` (`HttpRequest.cs:2341-2357`), called by
`HttpWriter` around post-cache substitution (`HttpWriter.cs:932-933, 1355-1357,
1430-1431`), and it returns immediately when the worker request is not
`IIS7WorkerRequest`. So nothing breaks and nothing compresses.

On this host compression belongs to ASP.NET Core middleware placed *before*
`UseRehostWebForms`, which is terminal (`RehostWebFormsExtensions.cs:39-51`).
That works for the buffered body, which is written through `response.Body` at
commit (`ResponseSpool.cs:128-130`). One asymmetry to record: file segments go
out via `response.SendFileAsync` (`:123-124`), which response-compression
middleware does not intercept, so pages would compress and static/`TransmitFile`
responses would not. "Preserve the contract" here means *documenting* that
compression is host middleware, not a System.Web feature — there is no
Framework behavior to preserve.

### 4.3 Upgrades and WebSockets

Already an explicit failure with Framework's own diagnostic:
`HttpContext.GetWebSocketInitStatus` returns `RequiresIntegratedMode` when the
worker request is not an `IIS7WorkerRequest` (`HttpContext.cs:183-187`), and
`IsWebSocketRequest` turns that into
`PlatformNotSupportedException(SR.Requires_Iis_Integrated_Mode)`
(`:219-221`); `AcceptWebSocketRequest` takes the same path. The whole
`src/System.Web.ReferenceSource/WebSockets/` stack hangs off
`UnmanagedWebSocketContext` and `IIS.MgdAcceptWebSocket`
(`IIS7WorkerRequest.cs:2586-2620`), i.e. it is native-context-bound by
construction, and IIS's own detection was two server variables
(`WEBSOCKET_VERSION`, `IIS_WEBSOCK`) published by `iiswsock.dll`.

There is no partial version of this worth building: System.Web's WebSocket API
cannot be served without the native context. "Fail explicitly" is already true;
what is missing is a **test pinning it** and a compatibility row saying the
substitute is Kestrel's own `IHttpUpgradeFeature` / WebSocket middleware in
front of `UseRehostWebForms`. Cheap Kestrel scenario.

### 4.4 HTTP/2 and HTTP/3

Only one member is protocol-aware: `GetHttpVersion()` passes Kestrel's
`Request.Protocol` through (`AspNetCoreWorkerRequest.cs:80-83`). Two
consequences:

* **Chunk framing.** On a *non-final* flush with no content length, System.Web
  appends `Transfer-Encoding: chunked` and writes chunk framing bytes into the
  body itself, gated on `protocol.Equals("HTTP/1.1")` exactly
  (`HttpResponse.cs:736-752, 791-803`). Over HTTP/1.1 the port therefore hands
  Kestrel both a `Transfer-Encoding: chunked` header and pre-framed bytes;
  Kestrel honors an app-set transfer encoding instead of framing again, which
  is why `HeaderAmendmentOverKestrelTests.cs:114-126` sees a clean body and
  `ResponseHeadersOverKestrelTests.cs:178-195` sees the header in the mirror.
  Over h2/h3 the equality fails, so neither header nor framing is emitted —
  accidentally correct, because `Transfer-Encoding` is illegal there. This is
  load-bearing behavior resting on a string comparison; a h2 test would pin it.
* **`SERVER_PROTOCOL`.** The port reports `"HTTP/2"`. What IIS 10 reported for
  an HTTP/2 request is a reading (R3), and it feeds `Request.ServerVariables`
  and `Request.SaveAs` raw output (`HttpRequest.cs:2947`).

Request bodies are protocol-agnostic in the adapter: `RequestBodyCoordinator`
reads `Request.BodyReader` and `IsEntireEntityBodyIsPreloaded` keys on
`IHttpRequestBodyDetectionFeature` (`AspNetCoreWorkerRequest.cs:39, 234-237`),
both of which h2/h3 implement. `GetTotalEntityBodyLength` is the base
`Content-Length` parse (`WorkerRequest.cs:721-735`) and has no in-tree consumer.
Nothing is known-broken; the gap is that no test runs over h2/h3.

HTTP/2 over cleartext (h2c) on a loopback listener makes the existing body
suite runnable with no TLS setup — cheap. HTTP/3 needs MsQuic plus TLS and
platform support that differs across the three targets; a boundary is the
honest answer for now.

### 4.5 Streaming and file send

* `FlushResponse` is deliberately empty (`AspNetCoreWorkerRequest.cs:307-311`),
  per ADR 0003. `SupportsAsyncFlush` is not overridden → `false`
  (`WorkerRequest.cs:753`), so `Response.SupportsAsyncFlush` is false
  (`HttpResponse.cs:837-841`) and `FlushAsync` degrades to the synchronous path
  (`:846-880`). Byte order is preserved — an intermediate flush still calls
  `_httpWriter.Send(_wr)` (`HttpResponse.cs:805`) — but nothing reaches the
  client before `EndOfRequest`. So `Response.BufferOutput = false`, progress
  pages, server-sent events, and long downloads all buffer wholly (and spill to
  disk above 32 KiB, §3).
* `SendResponseFromFile(string, long, long)` is honored via `SendFileAsync`
  (`AspNetCoreWorkerRequest.cs:296-299`, `ResponseSpool.cs:87-94, 119-131`),
  which is how static serving and `Response.TransmitFile` already work
  (`StaticFileHandler.cs:610-613`).
* `SendResponseFromFile(IntPtr, long, long)` throws `NotSupportedException`
  (`AspNetCoreWorkerRequest.cs:301-305`) where IIS7 opened a `FileStream` over
  the handle (`IIS7WorkerRequest.cs:1060-1088`). Reached by
  `Response.WriteFile(IntPtr, long, long)` (`HttpResponse.cs:3075`).
  `SafeFileHandle` works on Unix, so this is implementable, but a raw `IntPtr`
  file handle in a Web Forms app is vanishingly rare — an explicit failure is
  defensible; it just needs a compatibility row.
* `SupportsLongTransmitFile` is not overridden → `false`
  (`WorkerRequest.cs:986-988`), so `HttpFileResponseElement` refuses an offset
  or size above `Int32.MaxValue` with `ArgumentOutOfRangeException`
  (`HttpWriter.cs:517-529`) where IIS7 allowed it (`IIS7WorkerRequest.cs:998`).
  `SendFileAsync` takes `long`, so this is a one-line override.
* `HeadersSent()` is not overridden → base `true` (`WorkerRequest.cs:1093-1095`).
  Harmless: nothing in the imported tree calls it.

---

## 5. Proposed order of work

### Tier A — cheap, valuable, decidable from source (no reading)

1. **Override `IsSecure()`** from `Request.IsHttps`. Fixes
   `Request.IsSecureConnection`, `Request.Url.Scheme`, `SERVER_PORT_SECURE`,
   and `GetProtocol()` in one line. Unit-testable with a fake `HttpContext`.
2. **Override `GetServerName()`** (and consider `GetLocalPortAsString()`) from
   the `Host` header, falling back to the local address when absent, so
   `Request.Url` and `SERVER_NAME` carry the authority the client asked for.
   Unit-testable. (Reading R2 refines the fallbacks but is not needed to stop
   emitting a loopback IP.)
3. **Delete or align the shadowing arms** of the adapter's `GetServerVariable`
   (`SERVER_NAME`, `SERVER_PORT`, `SERVER_PORT_SECURE`, `PATH_INFO`) so one
   answer exists per variable. Unit-testable.
4. **Override `SupportsLongTransmitFile => true`.** Unit-testable.
5. **Response-spill scenario + cleanup matrix** (§3.2). Kestrel scenario plus a
   temp-directory census; needs a decision on whether the threshold becomes
   configurable.
6. **Pin the WebSocket boundary** with a Kestrel scenario asserting
   `PlatformNotSupportedException` from `Context.IsWebSocketRequest`, and add
   the compatibility row naming Kestrel middleware as the substitute.
7. **HTTP/2 (h2c) round of the existing body suite** on a loopback listener.

### Tier B — needs an IIS reading first

Rig: full IIS 10 + Framework 4.8 on `winbox`, raw-socket sends, a probe handler
in the shape of `PathProbeHandler` extended to print
`Request.ServerVariables` and `Request.Headers`.

| id | reading |
|---|---|
| R1 | Exact strings for `SERVER_SOFTWARE`, `GATEWAY_INTERFACE`, `APPL_MD_PATH`, `INSTANCE_ID`, `INSTANCE_META_PATH`, `APP_POOL_ID` on a default site and on a non-default site/app-pool. |
| R2 | `SERVER_NAME`, `SERVER_PORT`, `SERVER_PORT_SECURE`, `HTTPS`, `Request.Url` under: `Host` header matching the binding, `Host` differing from the binding, absent `Host` (HTTP/1.0), non-default port, and an HTTPS binding. |
| R3 | `SERVER_PROTOCOL` and `Request.Url` for an HTTP/2 request to IIS 10. |
| R4 | Raw response bytes for a header value containing `é` and `€`, with `responseHeaderEncoding` unset, `iso-8859-1`, `utf-8`, `windows-1252`; also a non-ASCII header *name*. |
| R5 | Raw request with `X-T: 0xE9`, with `X-T: 0xC3 0xA9`, and with invalid UTF-8 — record `Request.Headers["X-T"]`, `ServerVariables["HTTP_X_T"]`, and `ServerVariables["ALL_RAW"]` as code points. Settles ANSI vs Latin-1 and whether the two paths agree. |
| R6 | With a mapped client certificate: `CERT_FLAGS`, `CERT_SUBJECT`, `CERT_ISSUER`, `CERT_SERIALNUMBER`, `CERT_COOKIE`, `CERT_KEYSIZE`, `CERT_SECRETKEYSIZE`, and `Request.ClientCertificate.{Certificate,ValidFrom,ValidUntil,BinaryIssuer,PublicKey,CertEncoding}`. |
| R7 | Two identical header lines (`X-R: a` then `X-R: b`): what `GetUnknownRequestHeader`, `ALL_RAW`, `ALL_HTTP`, and `HTTP_X_R` show. Confirms or corrects the `", "` join. |
| R8 | For a path-info URL, the full `ServerVariables` dump (`PATH_INFO`, `PATH_TRANSLATED`, `SCRIPT_NAME`, `URL`, `UNENCODED_URL`, `HTTP_URL`). Extends the P72 reading set to the variable surface. |

Work unblocked by them: `responseHeaderEncoding` translation (R4), request-header
decoding policy (R5), client-certificate translation (R6), the IIS-only variable
verdicts (R1, R2, R8), header-join fidelity (R7).

### Tier C — record as boundaries, do not build

* WebSockets and protocol upgrades through `System.Web` (native-context bound).
* Client-visible streaming / incremental flush (ADR 0003 already chose
  single-shot commit; the consequence needs stating in `compatibility.md`).
* HTTP/3.
* `ServerVariables.Set` and rewrite-module variables
  (`PlatformNotSupportedException`, Framework's own).
* Windows-auth variables `LOGON_USER`, `AUTH_PASSWORD`, and `GetUserToken` /
  `GetVirtualPathToken` (impersonation is already inert, ledger P07).
* `INSTANCE_*`, `APPL_MD_PATH`, `APP_POOL_ID`, `UNENCODED_URL`, `HTTP_URL`,
  `CACHE_URL`, `SCRIPT_TRANSLATED` — IIS-only, and unreachable through the
  collection off IIS7 anyway.
* `SendResponseFromFile(IntPtr, …)`.
* Compression as host middleware, with the `SendFileAsync` asymmetry noted.

---

## 6. Decisions needed

1. **Should `Request.IsSecureConnection` and `Request.Url` tell the truth?**
   Today they say "http" and the server's IP address on every request.
   *Recommendation: yes — override `IsSecure()` and `GetServerName()` in the
   adapter. Two small, unit-testable changes with the widest payoff here.*

2. **Where should `SERVER_NAME` / `SERVER_PORT` come from — the `Host` header or
   the socket?** IIS used the `Host` header; the port currently uses both,
   inconsistently, in different places.
   *Recommendation: `Host` header with a socket fallback, matching IIS, and
   delete the disagreeing code path.*

3. **Do IIS-only server variables answer `null`, or do we invent plausible
   values (`SERVER_SOFTWARE`, `GATEWAY_INTERFACE`, `APPL_MD_PATH`)?**
   *Recommendation: answer `null` for anything describing IIS's own topology,
   but supply `GATEWAY_INTERFACE = "CGI/1.1"` and a `SERVER_SOFTWARE` naming
   this host — they are pure strings some apps log or branch on.*

4. **Is `<globalization responseHeaderEncoding>` worth implementing, given
   Kestrel's encoder is process-wide?** The faithful fix is the byte-transparent
   Latin-1 commit described in §2.2.
   *Recommendation: yes, but after reading R4 — it is roughly twenty lines and
   removes a "Partial" from the compatibility map.*

5. **What should non-ASCII request headers decode as?** Kestrel currently uses
   ASCII-then-strict-UTF-8 (400 on bad bytes); IIS's answer is unmeasured.
   *Recommendation: take reading R5 first, then match it; do not change the
   default on a guess.*

6. **Should the response-spill threshold become a host option?** A scenario test
   at the real 32 KiB is honest but slow to write for every cleanup case.
   *Recommendation: keep 32 KiB in production, expose the threshold internally
   so the cleanup matrix can run at a few bytes, and keep one full-size
   end-to-end test.*

7. **Should client certificates be translated from Kestrel's TLS feature?**
   Today `Request.ClientCertificate.IsPresent` is always `false`, which is a
   correct-shaped empty rather than a wrong value.
   *Recommendation: yes — it is a contained adapter change and the only way a
   certificate-authenticated app can work — but do it after reading R6 so the
   `CERT_*` string formats match.*

8. **WebSockets: fail explicitly, or wire Kestrel's own upgrade path?** The
   `System.Web` API cannot be served without IIS's native context.
   *Recommendation: keep the existing `PlatformNotSupportedException`, add a
   test that pins it, and document Kestrel middleware as the substitute.*

9. **Do we gate HTTP/2 and HTTP/3, or claim them?**
   *Recommendation: run the existing body suite over h2c and claim HTTP/2;
   record HTTP/3 as an untested boundary.*

10. **Is client-visible streaming (an incremental `Response.Flush` reaching the
    wire) in scope at all?** ADR 0003 chose a single commit, which makes
    streaming impossible by construction.
    *Recommendation: leave it out of scope and say so in `compatibility.md`
    rather than leaving it "Unassessed" — it is a decision already taken, not a
    gap.*

11. **`SendResponseFromFile(IntPtr, …)` and `SupportsLongTransmitFile`.**
    *Recommendation: turn on `SupportsLongTransmitFile` (one line, removes a
    spurious failure on files over 2 GB) and leave the native-handle overload
    throwing.*

## Framework readings

IIS 10 + .NET Framework 4.8.9344 on winbox, 2026-08-16. One site with an http
binding (8112) and an https binding (8453, self-signed `CN=svars.local`), a
sibling site with `<globalization responseHeaderEncoding="iso-8859-1"/>`
(8113); a page dumping `Request.Url`, `IsSecureConnection`, `UserHostAddress`,
`PathInfo`, every `Request.ServerVariables` entry, every `Request.Headers`
entry, and the char codes of `X-Probe`; a second page appending `X-Accent:
café 中` and a `Content-Disposition` filename with `ï`/`ü`. Requests were raw
sockets through an ssh tunnel (so REMOTE_ADDR is 127.0.0.1 and the Host
header's port differs from the binding's, which is what exposed the SERVER_PORT
rule).

**R1 — server variables (http, `Host: 127.0.0.1:18112`, `?q=1`).** 45 entries.
`Url=http://127.0.0.1:8112/Default.aspx?q=1`; `SERVER_NAME=127.0.0.1` (Host
host), `SERVER_PORT=8112` (binding, not the Host header's 18112),
`SERVER_PORT_SECURE=0`, `HTTPS=off`, `LOCAL_ADDR=127.0.0.1`,
`REMOTE_HOST=127.0.0.1`, `SERVER_PROTOCOL=HTTP/1.1`, `URL=/Default.aspx`,
`PATH_INFO=/Default.aspx` (full path; `Request.PathInfo=""`),
`PATH_TRANSLATED=<app>\Default.aspx`, `SCRIPT_NAME=/Default.aspx`,
`APPL_MD_PATH=/LM/W3SVC/2/ROOT`, `INSTANCE_ID=2`, `INSTANCE_META_PATH=/LM/W3SVC/2`,
`GATEWAY_INTERFACE=CGI/1.1`, `SERVER_SOFTWARE=Microsoft-IIS/10.0`,
`CONTENT_LENGTH=0`; `AUTH_*`, `LOGON_USER`, `REMOTE_USER`, `CERT_*`,
`HTTPS_KEYSIZE`, `HTTPS_SECRETKEYSIZE`, `HTTPS_SERVER_*`, `CONTENT_TYPE` all
`""` — none null. `Host: shop.example.com` → `Url=http://shop.example.com:8112/…`,
`SERVER_NAME=shop.example.com`. `/Default.aspx/extra/x?y=2` →
`PATH_INFO=/Default.aspx/extra/x`, `Request.PathInfo=/extra/x`,
`PATH_TRANSLATED` still `…\Default.aspx`.

**R2 — https (`Host: svars.local:8453`).** `Url=https://svars.local:8453/…`,
`IsSecureConnection=True`, `HTTPS=on`, `SERVER_PORT=8453`,
`SERVER_PORT_SECURE=1`, `HTTPS_KEYSIZE=256`, `HTTPS_SECRETKEYSIZE=2048`,
`HTTPS_SERVER_ISSUER=CN=svars.local`, `HTTPS_SERVER_SUBJECT=CN=svars.local`,
`CERT_SERVER_ISSUER/SUBJECT=CN=svars.local`, `CERT_FLAGS=""`.

**R3 — forwarded headers (`X-Forwarded-Proto: https`, `-For: 203.0.113.9`,
`-Host: front.example.com` over http).** IIS ignores them: `Url`, `HTTPS`,
`SERVER_NAME`, `REMOTE_ADDR` unchanged; they appear only as `HTTP_X_FORWARDED_*`.

**R4 — response header bytes.** Default: `X-Accent: caf\xC3\xA9 \xE4\xB8\xAD`,
`Content-Disposition: attachment; filename="na\xC3\xAFve \xC3\xBC.txt"`
(raw UTF-8), `Response.HeaderEncoding=utf-8`. `iso-8859-1`:
`X-Accent: caf\xE9 ?`, `filename="na\xEFve \xFC.txt"`.

**R5 — request header bytes (`X-Probe`).** `caf\xC3\xA9 \xE4\xB8\xAD` →
`café 中` (codes 99,97,102,233,32,20013); `caf\xE9` → `café` (…,233);
`a\xFF\xFEb` → `aÿþb` (97,255,254,98). All 200; nothing refused.

Port before P76/P77 on the same requests: `IsSecureConnection=false` and
`Url=http://<local IP>:<port>/…` for every request; `SERVER_NAME=<local IP>`;
IIS-native variables null; invalid UTF-8 request header → 400 from Kestrel;
`iso-8859-1` ignored (UTF-8 on the wire).
