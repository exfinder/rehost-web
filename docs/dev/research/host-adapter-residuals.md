# ASP.NET Core host adapter evidence

Evidence for ledger P76-P78 and the remaining
[host-adapter gaps](../follow-ups/aspnet-core-host-adapter.md).

## Result

P76 derives scheme and authority from the request, supplies the fixed portable
server-variable set, and enables trusted forwarded-header translation. P77
matches IIS request-header decoding and configured response-header encoding.
P78 covers long `TransmitFile`, response-spool cleanup, and HTTP/2.

Still unassessed: HTTP/3, forwarded client certificates, integrated-only server
variables, and `ServerVariables.Set`. Compression remains host/IIS-role policy.

## Framework readings

Captured 2026-08-16 on IIS 10 / Framework 4.8.9344 using HTTP and HTTPS sites
plus raw sockets.

| ID | Result |
| --- | --- |
| R1 | HTTP exposes 45 variables; host name follows `Host`, port follows binding; IIS topology variables have fixed metabase shapes; unset auth/certificate values are empty strings |
| R2 | HTTPS sets secure URL/port flags, key sizes, and server certificate subject/issuer values |
| R3 | IIS ignores `X-Forwarded-*` semantically and exposes them only as ordinary `HTTP_X_*` variables |
| R4 | Response headers default to UTF-8; `iso-8859-1` emits Latin-1 and replaces unmappable characters |
| R5 | Request headers decode valid UTF-8 as UTF-8 and otherwise Latin-1; invalid UTF-8 is not refused |

Notable R1 details: `SERVER_NAME` follows the Host host, `SERVER_PORT` the IIS
binding, `PATH_INFO` carries the full path while `Request.PathInfo` carries the
handler suffix, and `APPL_MD_PATH`, `INSTANCE_ID`, `INSTANCE_META_PATH`,
`GATEWAY_INTERFACE`, and `SERVER_SOFTWARE` are non-null.

Port behavior and divergences are maintained in the
[compatibility map](../compatibility.md), not this evidence snapshot.
