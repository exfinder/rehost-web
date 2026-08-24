# IIS configuration and response-header readings

Evidence for ledger P60/P67 and remaining IIS configuration tenants. Current
support lives in the [compatibility map](../compatibility.md); unresolved work
lives in the [IIS configuration follow-up](../follow-ups/iis-integration-plan.md).

## Configuration collections

Captured 2026-08-07 on IIS 10.0.26100. Schema source and retrieval details are
in [IIS configuration reference](../iis-config-reference.md).

| ID | Stimulus | Result |
| --- | --- | --- |
| C1 | Duplicate inherited `mimeMap` | 500.19 for the application |
| C2 | Remove absent extension | Tolerated |
| C3 | Clear then add `.css` | `.css` serves; others return 404.3 |
| C4 | Remove inherited `.css` | `.css` returns 404.3 |
| C5 | Add hidden segment `Private` | Case-insensitive 404.8 |
| C6 | Remove inherited `App_Data` hidden segment | `App_Data` serves |
| C7 | Folder-level `staticContent` | Folder mapping works; root unaffected |

## Default documents

Captured 2026-08-14 on IIS Express 10.0.26013. Golden order:
`Default.htm`, `Default.asp`, `index.htm`, `index.html`, `iisstart.htm`,
`default.aspx`; application additions prepend (`mergeAppend=false`).

| ID | Stimulus | Result |
| --- | --- | --- |
| D1 | `/` with only `Default.aspx`, list says `default.aspx` | 200; observed paths use list casing; `RawUrl` remains `/` |
| D2 | Directory request with query | Query preserved through rewrite |
| D3-D4 | Multiple or later candidates | First existing candidate in list order serves |
| D5 | Existing directory without trailing slash | 301 absolute location; query preserved |
| D6 | Directory without candidate, browsing off | 403.14 |
| D7 | Missing directory | 404.0 |
| D8 | Folder adds `custom.htm` | Added candidate wins over inherited list |
| D9 | Folder removes `index.html` | File is skipped; request returns 403.14 |
| D10 | Folder clears then adds | Only added candidate considered |
| D11 | Folder duplicates inherited add | Directory requests there return 500.19; direct files still serve |
| D12 | Folder disables default documents | Directory returns 403.14; direct files serve |
| D13 | Root duplicate add | Directory requests fail app-wide; direct files/pages serve |
| D14 | Slashless missing, empty, disabled directories | 404; 301; 403 respectively |
| D15 | Root duplicate plus slashless directory | 500, no redirect |

The port deliberately validates honored sections at activation instead of
reproducing IIS's consumption-scoped configuration failures.

## `Response.Headers`

Captured 2026-08-15 on IIS Express 10.0.26013. The `Server` header is omitted
from observations below.

| ID | Stimulus | Result |
| --- | --- | --- |
| H1-H5 | No header mutation; content type, cookies, redirect, cache APIs | Collection stays empty; generated values appear on wire |
| H6 | Append duplicate custom header | Both values stored and sent |
| H7 | Set `Location` in collection | Sent as written; does not set `RedirectLocation` |
| H8 | Set `Content-Type` in collection | `Response.ContentType` wins at send |
| H9 | Set `Cache-Control` in collection | Cache policy wins at send |
| H10 | Remove generated cookie header | Cookies still append at send |
| H11 | Remove generated redirect location | `RedirectLocation` still emits |
| H12 | Remove generated cache/version headers | Generated values still emit |
| H13 | Append then remove custom header | Header not sent |
| H14 | Add literal `Set-Cookie` | Literal value sent |
| H15 | Mutate after flush | Add throws; remove is inert; reads work |
| H16 | Clear headers | Collection empties; generated headers return at send |

Before send, the collection owns only entries written through it. At send,
content type, cache policy, redirect location, and cookies layer over those
entries. After first flush it mirrors the emitted block and becomes immutable.
