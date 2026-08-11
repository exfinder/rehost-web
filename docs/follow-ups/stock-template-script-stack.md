# Stock template script stack

Milestone 1 first restores the frozen Visual Studio template's existing
full-page script and style path. This is deliberately smaller than general
ASP.NET AJAX support.

## Application boundary

`apps/WebFormsApplication/WebFormsApplication` remains unchanged. Its C#,
markup, configuration, and committed assets are inputs. Rehost sidecars,
locally built packages, and deterministic changes to the staged configuration
may adapt those inputs for .NET 10.

The first result supports the template's existing `ScriptManager`, named
bundles, jQuery mapping, and `BundleReference`. It uses the physical JS and CSS
already committed by the legacy NuGet installation. It does not claim default
embedded-script delivery or partial-page updates.

## Components

| Component | Initial implementation |
| --- | --- |
| `Rehost.WebForms.Extensions` | Original `System.Web.*` namespaces; compile the `ScriptManager`, script-reference/mapping, full-page lifecycle, ordering, deduplication, debug-path, and Optimization-adapter closure |
| `Rehost.WebForms.Optimization` | Import the official source and attempt its full compile; only frozen-template behavior becomes supported |
| `Rehost.WebForms.Optimization.WebForms` | Port `BundleReference`; rewrite its original assembly name only in the staged application configuration |
| `Rehost.WebForms.ScriptManager.Bundles` | Register `MsAjaxBundle`, `WebFormsBundle`, and the 11 individual Microsoft AJAX names used automatically by ScriptManager; carry no JS content |
| WebGrease 1.6.0 | Consume unchanged for JS/CSS transforms, with repository Newtonsoft.Json and Antlr versions and a project-scoped `NU1701` suppression |

`Rehost.WebForms.Extensions` has direct friend access to Runtime, preserving the
original `System.Web`/`System.Web.Extensions` internal relationship without
adding public adapters. Rehost assembly identities remain distinct from the
Framework assemblies.

## Source layout

The official sibling checkouts are import authorities only. Builds use
committed local imports:

```text
src/System.Web.Extensions.ReferenceSource/
src/System.Web.Optimization.ReferenceSource/
src/Microsoft.AspNet.Web.Optimization.WebForms.ReferenceSource/

src/Rehost.WebForms.Extensions/
src/Rehost.WebForms.Optimization/
src/Rehost.WebForms.Optimization.WebForms/
src/Rehost.WebForms.ScriptManager.Bundles/
```

Available upstream trees are imported completely. Imported files stay unchanged
where practical; compatibility replacements live in Rehost project directories.
An explicit compile list limits Extensions to the reached closure. Optimization
gets a full-compile attempt first; compilation does not widen its compatibility
claim.

## Sequence

1. Pin and import ASP.NET Web Optimization; attempt full .NET 10 compilation of
   Optimization and its WebForms control.
2. Import `System.Web.Extensions`; establish Runtime friend access and compile
   the frozen-template closure.
3. Add the script registration package and observe the original packages'
   startup mappings where source is unavailable.
4. Replace legacy package references in the Rehost sidecar and rewrite the one
   staged WebForms Optimization assembly reference.
5. Build and run the unchanged application, then validate all emitted assets.

Any need to mutate frozen application sources, introduce another Runtime seam,
change an agreed assembly/package identity, or widen/narrow compiled feature
scope requires approval before implementation continues.

## Evidence

The slice is complete when:

- the unchanged application builds from locally produced packages;
- its navigation pages run over Kestrel;
- rendered JS/CSS order is asserted and every emitted asset URL returns 200;
- one real-browser pass is visually correct and console-clean; and
- build plus HTTP smoke passes on Windows x64, Linux x64, and macOS arm64.

Deferred behavior remains indexed in [`../backlog.md`](../backlog.md).

## Current result

The unchanged application builds from local packages and `/Default` renders on
macOS arm64. Both generated bundles and all six remaining emitted static assets
return 200. Embedding is not optional for a physically served script: original
ScriptManager semantics validate the manifest resource before rendering the
physical path, so both runtime and Extensions embed every resource they declare.

The remaining completion gates are an automated journey and a real-browser pass;
the Linux round now covers this work.
