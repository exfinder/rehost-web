# In-place development run

The dev and publish flows both run from a staged copy of the site
(`rehost_root/`, see [migration stages](migration-stages-and-site-layout.md)).
The cost is the classic Web Forms inner loop: editing an `.aspx` requires a
(fast, no-compile) build to refresh the stage before the browser sees it. On
IIS/Framework, markup edits were visible on refresh with no build step. The gap
exists only while the Framework project is alive beside the port; once the
content moves into `rehost_root/` the runtime serves the source folder and
recompiles an edited page on its own.

## Content sync (no runtime change)

Watch `RehostSiteContentRoot` and copy changed content files into
`rehost_root/` without a build: not the payload, not `web.config`. The
runtime's page recompile does the rest, which restores the Framework loop for
markup. Code-behind edits need a rebuild, as they did in a Framework WAP.

## Host-provided bin seam (runtime change)

Running the port in place from the legacy folder would restore that loop, but
the runtime treats `bin/` under the physical root as the application's
compilation references, and the legacy folder's `bin/` holds Framework-built
assemblies that the Framework toolchain keeps rebuilding while the legacy app
stays alive side by side. In-place therefore needs a designed seam: the host
supplies the reference/bin location explicitly, diverging from
physical-root/`bin` for the first time.

Sketch: a host option (`ApplicationBinPath` or similar) that BuildManager and
assembly resolution honor instead of `<physicalRoot>/bin`, with the legacy
`bin/` explicitly ignored. Decision points: how page compilation references
interact with `AppContext.BaseDirectory` loading, whether `PreApplicationStart`
scanning follows the override, and what Framework behavior says about
`PrivateBinPath` fidelity. This is runtime architecture, not app plumbing —
design before implementing.
