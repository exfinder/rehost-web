# Configuration-error readings

What Framework and IIS return when an application's `web.config` is broken, and
which of the two answers. Taken because the port returns `200` with an empty
body for a configuration error raised from `HttpRuntime.HostingInit`
([backlog](../backlog.md)), and nothing recorded what it should return instead.

Captured 2026-09-07 on winbox: IIS Express 10.0.26013.1000, .NET Framework
4.8.09221, `System.Web.dll` 4.8.9344.0. Each probe is its own site holding one
`Default.aspx` and the stated `web.config`; both requests are
`GET /Default.aspx` from the same machine, the second immediately after the
first.

| `web.config` | Status | Body | Second request |
| --- | --- | --- | --- |
| baseline, nothing broken | `200` | the page | identical |
| `<system.net><mailSettings>` with an smtp host | `200` | the page | identical |
| undeclared `<madeUpSection>` | `500` | IIS **500.19** detailed error, `0x80070032`, "cannot be read because it is missing a section declaration", handler "Not yet determined" | identical |
| `<authentication mode="Nonsense">` | `500` | ASP.NET **Configuration Error** page | identical |
| `<appSettings configSource="nope.config">` | `500` | ASP.NET **Configuration Error** page | identical |
| section declared with an unresolvable handler type, never accessed | `200` | the page | identical |

## Who answers

Two different components, and which one answers decides the body.

IIS parses `web.config` natively before any managed code runs, and a section it
cannot resolve is refused there: `500.19` from IIS Web Core, no
`X-AspNet-Version` header, and the handler never determined. `machine.config`'s
`<configSections>` reach that parser, which is why `<system.net>` passes it.

Everything IIS accepts is parsed again by the managed configuration system, and
its failures render ASP.NET's **Configuration Error** page: `500`, the standard
description, a `Parser Error Message`, and a Source Error block quoting the
offending file with the failing line in red. The `configSource` probe returns
`Unable to open configSource file 'nope.config'.`

A broken section that nothing reads costs nothing. The handler type is resolved
on first access, so a declaration naming a missing type serves `200` until
something asks for the section.

## What the port must match

The port has no native IIS parser, so the managed answer is the one it owes: a
`500` carrying a page that names the configuration file and the parse failure.
Both readings repeat identically on the next request, so the failure is not
one-shot. Five requests against the `authentication mode="Nonsense"` site, one
of them to a different URL, returned byte-identical 5,270-byte pages: 249 ms
for the first, 26-27 ms for each later one (2026-09-08). A replay of cached
bytes would take a millisecond; 27 ms is a fresh parse and render, which is what
the imported `HostingInitFailed` path prescribes, an AppDomain shutdown after
each request and a rebuild on the next. The rebuild itself is not directly
observable from outside; the timing and the imported code agree.

The port answers the managed page (ledger P96).
