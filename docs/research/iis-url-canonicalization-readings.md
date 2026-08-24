# IIS URL canonicalization and path-info readings

Wire + handler-visible readings behind ledger P72: 127 raw request targets sent over a
TCP socket (no client-side URL normalization) to full IIS 10 + .NET Framework 4.8 on
`winbox` (2026-08-16; site `pathmap` on port 8161, `DefaultAppPool`, default request
filtering, `httpErrors errorMode="Detailed"`, a junction `link` → a directory outside the
site, an application `/app` beside a plain folder `/app2`), and the same list to the port
(macOS, `ScenarioHost --serve`, same probe assembly and files, after the P72 changes).
The probe is `Rehost.WebForms.Parity.Probes.PathProbeHandler` mapped as `*.probe`
(`resourceType="Unspecified"` on IIS), printing every path view a handler has of its
request; static rows are real files. `/app` rows are not comparable (the port hosts a
single application) and are kept for the IIS column only.

Column key: `IIS n.m` = IIS substatus from the detailed error page (`http.sys` = refused
by the kernel driver before IIS); `ASP.NET nnn` = the managed error page; `=` = the port
column matches the IIS column. Handler rows show what the handler saw; `RawUrl` is shown
only where it differs from `Path` (it never did on IIS, whose "raw" URL is the canonical
decoded path plus the verbatim query). Where the port column shows a `PhysicalPath`
casing different from IIS, IIS returned the URL's casing (its map-path cache serves the
first spelling requested) and the port the real one on a case-sensitive filesystem
(ledger P57).

Remaining differences and their owners: `..` inside the root and every handler-visible
value match; above-root climbs are 403 on both (host front door). Invalid Windows name
characters, double escaping and bad UTF-8 in a *static* URL are 404 from IIS's native
`StaticFileModule`/`RequestFilteringModule` and 400 from Framework's own `ValidatePath`
in the port, which serves static files through the managed pipeline. Denied extensions
now match IIS's 404 through ledger P86. A static file with a trailing separator is an IIS 500.0 (`ERROR_DIRECTORY`) versus
404; a raw `#` or raw non-ASCII byte is refused by http.sys (400) and by Kestrel/ASP.NET
(404/400) — all recorded on the [IIS-role follow-up](../follow-ups/iis-role-behaviors.md).
An NFD spelling of an NFC file name is found only on APFS (macOS); a trailing space on
an existence-agnostic handler URL runs the handler on the port and is unmappable on IIS
([filesystem semantics](../filesystem-semantics.md)). 8.3 short names are not served by
IIS either.


## baseline

| request | IIS + Framework | port |
|---|---|---|
| `/echo.probe` | 200 `Path=/echo.probe` `FilePath=/echo.probe` | = |
| `/sub/echo.probe` | 200 `Path=/sub/echo.probe` `FilePath=/sub/echo.probe` | = |
| `/x.txt` | 200 body `static:site/x.txt` | = |
| `/sub/x.txt` | 200 body `static:site/sub/x.txt` | = |
| `/ECHO.PROBE` | 200 `Path=/ECHO.PROBE` `FilePath=/ECHO.PROBE` | = |
| `/SUB/X.TXT` | 200 body `static:site/sub/x.txt` | = |

## 1 dot segments

| request | IIS + Framework | port |
|---|---|---|
| `/sub/../echo.probe` | 200 `Path=/echo.probe` `FilePath=/echo.probe` | = |
| `/../echo.probe` | 403 http.sys "Forbidden URL" | 403 (host front door) |
| `/./echo.probe` | 200 `Path=/echo.probe` `FilePath=/echo.probe` | = |
| `/sub/./echo.probe` | 200 `Path=/sub/echo.probe` `FilePath=/sub/echo.probe` | = |
| `/echo.probe/.` | 200 `Path=/echo.probe/` `FilePath=/echo.probe` `PathInfo=/` | = |
| `/echo.probe/..` | 403 IIS 403.14 | 403 ASP.NET 403 (forbidden handler) |
| `/sub/..` | 403 IIS 403.14 | 403 ASP.NET 403 (forbidden handler) |
| `/sub/../../../x.txt` | 403 http.sys "Forbidden URL" | 403 (host front door) |
| `/..` | 403 http.sys "Forbidden URL" | 403 (host front door) |
| `/sub/../../outside/x.txt` | 403 http.sys "Forbidden URL" | 403 (host front door) |

## 2 percent encoding

| request | IIS + Framework | port |
|---|---|---|
| `/sub/%2e%2e/echo.probe` | 200 `Path=/echo.probe` `FilePath=/echo.probe` | = |
| `/sub%2f..%2fecho.probe` | 200 `Path=/echo.probe` `FilePath=/echo.probe` | = |
| `/sub%2Fecho.probe` | 200 `Path=/sub/echo.probe` `FilePath=/sub/echo.probe` | = |
| `/sub%2Fx.txt` | 200 body `static:site/sub/x.txt` | = |
| `/sub%5Cecho.probe` | 200 `Path=/sub/echo.probe` `FilePath=/sub/echo.probe` | = |
| `/sub%5Cx.txt` | 200 body `static:site/sub/x.txt` | = |
| `/echo%2eprobe` | 200 `Path=/echo.probe` `FilePath=/echo.probe` | = |
| `/%65cho.probe` | 200 `Path=/echo.probe` `FilePath=/echo.probe` | = |
| `/echo.probe%00.txt` | 400 http.sys 400 | 400 (Kestrel) |
| `/x.txt%00.jpg` | 400 http.sys 400 | 400 (Kestrel) |
| `/echo.probe%25` | 404 IIS 404.0 | 400 ASP.NET 400 (invalid path character) |
| `/x.txt%25` | 404 IIS 404.0 | 400 ASP.NET 400 (invalid path character) |
| `/%252e%252e/x.txt` | 404 IIS 404.11 | 400 ASP.NET 400 (invalid path character) |
| `/sub/%252e%252e/x.txt` | 404 IIS 404.11 | 400 ASP.NET 400 (invalid path character) |
| `/echo.probe+x` | 404 IIS 404.11 | 404 ASP.NET 404 |
| `/sub+dir/x.txt` | 404 IIS 404.11 | 404 ASP.NET 404 |
| `/sub%20dir/x.txt` | 200 body `static:site/sub` | = |
| `/sub dir/x.txt` | 400 http.sys 400 | 400 (Kestrel) |
| `/sub%20dir/echo.probe` | 200 path=/sub dir/echo.probe file=/sub dir/echo.probe info= phys | = |
| `/x%2Etxt` | 200 body `static:site/x.txt` | = |
| `/%78.txt` | 200 body `static:site/x.txt` | = |

## 3 backslash

| request | IIS + Framework | port |
|---|---|---|
| `/sub\echo.probe` | 200 `Path=/sub/echo.probe` `FilePath=/sub/echo.probe` | = |
| `/sub\x.txt` | 200 body `static:site/sub/x.txt` | = |
| `\sub\echo.probe` | 400 http.sys 400 | 400 (Kestrel) |
| `\x.txt` | 400 http.sys 400 | 400 (Kestrel) |
| `/sub/..\..\web.config` | 403 http.sys "Forbidden URL" | 403 (host front door) |
| `/sub\..\web.config` | 404 IIS 404.8 | 403 ASP.NET 403 (forbidden handler) |
| `/sub\..\x.txt` | 200 body `static:site/x.txt` | = |
| `/x.txt\` | 500 IIS 500.0 | 404 ASP.NET 404 |
| `/echo.probe\` | 200 `Path=/echo.probe/` `FilePath=/echo.probe` `PathInfo=/` | = |
| `/echo.probe\extra` | 200 `Path=/echo.probe/extra` `FilePath=/echo.probe` `PathInfo=/extra` | = |

## 4 repeated slashes

| request | IIS + Framework | port |
|---|---|---|
| `//echo.probe` | 200 `Path=/echo.probe` `FilePath=/echo.probe` | = |
| `//x.txt` | 200 body `static:site/x.txt` | = |
| `/sub//echo.probe` | 200 `Path=/sub/echo.probe` `FilePath=/sub/echo.probe` | = |
| `/sub//x.txt` | 200 body `static:site/sub/x.txt` | = |
| `///echo.probe` | 200 `Path=/echo.probe` `FilePath=/echo.probe` | = |
| `/echo.probe//` | 200 `Path=/echo.probe/` `FilePath=/echo.probe` `PathInfo=/` | = |
| `/x.txt//` | 500 IIS 500.0 | 404 ASP.NET 404 |

## 5 windows name rules on last segment

| request | IIS + Framework | port |
|---|---|---|
| `/echo.probe.` | 404 ASP.NET 404 | = |
| `/x.txt.` | 404 ASP.NET 404 | = |
| `/echo.probe%20` | 404 IIS 404.0 | 200 `Path=/echo.probe␠` `FilePath=/echo.probe␠` (handler ran) |
| `/x.txt%20` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/echo.probe%2E` | 404 ASP.NET 404 | = |
| `/echo.probe::$DATA` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/x.txt::$DATA` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/x.txt:stream` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/x.txt%3A%3A%24DATA` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/x.txt%3C` | 404 IIS 404.0 | 400 ASP.NET 400 (invalid path character) |
| `/x.txt%3E` | 404 IIS 404.0 | 400 ASP.NET 400 (invalid path character) |
| `/x.txt%7C` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/x.txt%22` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/x.txt*` | 404 IIS 404.0 | 400 ASP.NET 400 (invalid path character) |
| `/x.txt%2A` | 404 IIS 404.0 | 400 ASP.NET 400 (invalid path character) |
| `/x.txt%3F` | 404 IIS 404.0 | 400 ASP.NET 400 (invalid path character) |
| `/x.txt?a` | 200 body `static:site/x.txt` | = |
| `/echo.probe?a=%2F..%2Fx` | 200 `Path=/echo.probe` `FilePath=/echo.probe` | = |
| `/echo.probe?../x` | 200 `Path=/echo.probe` `FilePath=/echo.probe` | = |
| `/x.txt#frag` | 400 http.sys 400 | 404 ASP.NET 404 |
| `/echo.probe%23frag` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/x.txt%2F` | 500 IIS 500.0 | 404 ASP.NET 404 |

## 6 unicode

| request | IIS + Framework | port |
|---|---|---|
| `/résumé.txt` | 404 IIS 404.0 | 400 (Kestrel) |
| `/r%C3%A9sum%C3%A9.txt` | 200 body `static:site/résumé.txt` | = |
| `/re%CC%81sume%CC%81.txt` | 404 IIS 404.0 | 200 body `static:site/résumé.txt` |
| `/r%C3%A9sum%C3%A9.probe` | 200 `Path=/résumé.probe` `FilePath=/résumé.probe` | = |
| `/re%CC%81sume%CC%81.probe` | 200 `Path=/résumé.probe` `FilePath=/résumé.probe` | = |
| `/R%C3%89SUM%C3%89.txt` | 200 body `static:site/résumé.txt` | = |
| `/%E2%80%AEx.txt` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/x%C2%A0.txt` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/%FF.txt` | 404 IIS 404.0 | 400 ASP.NET 400 (invalid path character) |
| `/x.txt%C0%AF` | 404 IIS 404.0 | 400 ASP.NET 400 (invalid path character) |

## 7 sibling prefix and sub-app

| request | IIS + Framework | port |
|---|---|---|
| `/app/echo.probe` | 200 `Path=/app/echo.probe` `FilePath=/app/echo.probe` | = |
| `/app/x.txt` | 200 body `static:app/x.txt` | 404 ASP.NET 404 |
| `/app2/echo.probe` | 200 `Path=/app2/echo.probe` `FilePath=/app2/echo.probe` | = |
| `/app2/x.txt` | 200 body `static:site/app2/x.txt` | = |
| `/app/../app2/x.txt` | 200 body `static:site/app2/x.txt` | = |
| `/app/../echo.probe` | 200 `Path=/echo.probe` `FilePath=/echo.probe` | = |
| `/app/../x.txt` | 200 body `static:site/x.txt` | = |
| `/app%2f..%2fx.txt` | 200 body `static:site/x.txt` | = |
| `/app/sub/../echo.probe` | 200 `Path=/app/echo.probe` `FilePath=/app/echo.probe` | = |
| `/apP/echo.probe` | 200 `Path=/apP/echo.probe` `FilePath=/apP/echo.probe` | = |

## 8 junction

| request | IIS + Framework | port |
|---|---|---|
| `/link/x.txt` | 200 body `static:outside/x.txt` | = |
| `/link/echo.probe` | 200 `Path=/link/echo.probe` `FilePath=/link/echo.probe` | = |
| `/link/../x.txt` | 200 body `static:site/x.txt` | = |
| `/link/../../outside/x.txt` | 403 http.sys "Forbidden URL" | 403 (host front door) |

## 9 path-info and extension boundary

| request | IIS + Framework | port |
|---|---|---|
| `/echo.probe/extra/info` | 200 `Path=/echo.probe/extra/info` `FilePath=/echo.probe` `PathInfo=/extra/info` | = |
| `/echo.probe/extra/info.txt` | 200 `Path=/echo.probe/extra/info.txt` `FilePath=/echo.probe` `PathInfo=/extra/info.txt` | = |
| `/sub/echo.probe/a.b` | 200 `Path=/sub/echo.probe/a.b` `FilePath=/sub/echo.probe` `PathInfo=/a.b` | = |
| `/echo.probe/` | 200 `Path=/echo.probe/` `FilePath=/echo.probe` `PathInfo=/` | = |
| `/echo.probe.bak` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/echo.probe.config` | 404 IIS 404.7 | 403 ASP.NET 403 (forbidden handler) |
| `/x.txt/extra` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/x.txt/` | 500 IIS 500.0 | 404 ASP.NET 404 |
| `/sub` | 301 body `<head><title>Document` `Location: /sub/` | 301 `Location: /sub/` |
| `/sub/` | 403 IIS 403.14 | 403 ASP.NET 403 (forbidden handler) |
| `/sub%2Fecho.probe%2Fextra` | 200 `Path=/sub/echo.probe/extra` `FilePath=/sub/echo.probe` `PathInfo=/extra` | = |
| `/echo.probe%2Fextra` | 200 `Path=/echo.probe/extra` `FilePath=/echo.probe` `PathInfo=/extra` | = |
| `/echo.probe/extra%2Fmore` | 200 `Path=/echo.probe/extra/more` `FilePath=/echo.probe` `PathInfo=/extra/more` | = |
| `/echo.probe/../x.txt` | 200 body `static:site/x.txt` | = |
| `/echo.probe/extra/../..` | 403 IIS 403.14 | 403 ASP.NET 403 (forbidden handler) |
| `/nothere.probe` | 200 `Path=/nothere.probe` `FilePath=/nothere.probe` | = |
| `/nothere.probe/extra` | 200 `Path=/nothere.probe/extra` `FilePath=/nothere.probe` `PathInfo=/extra` | = |
| `/sub/nothere.probe` | 200 `Path=/sub/nothere.probe` `FilePath=/sub/nothere.probe` | = |
| `/nothere.txt` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/web.config` | 404 IIS 404.8 | 403 ASP.NET 403 (forbidden handler) |
| `/sub/secret.config` | 404 IIS 404.7 | 403 ASP.NET 403 (forbidden handler) |
| `/bin/Rehost.WebForms.Parity.Probes.dll` | 404 IIS 404.8 | 404 ASP.NET 404 |
| `/echo.probe/bin/x` | 200 `Path=/echo.probe/bin/x` `FilePath=/echo.probe` `PathInfo=/bin/x` | = |

## 10 short names

| request | IIS + Framework | port |
|---|---|---|
| `/LONGFI~1.PRO` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/LONGFI~1.TXT` | 404 IIS 404.0 | 404 ASP.NET 404 |
| `/LongFileNameProbe.probe` | 200 `Path=/LongFileNameProbe.probe` `FilePath=/LongFileNameProbe.probe` | = |
| `/LongFileNameStatic.txt` | 200 body `static:site/LongFileNameStatic.txt` | = |
