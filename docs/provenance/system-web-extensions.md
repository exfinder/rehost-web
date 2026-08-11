# System.Web.Extensions source

The import authority is the sibling `../referencesource` checkout at revision
`ec9fa9ae770d522a5b5f0607898044b7478574a3`. Its `System.Web.Extensions` tree
object is `4980d036e77731ef47248b6c5d849acb5b69cefe`; all 368 files are copied to
`src/System.Web.Extensions.ReferenceSource`. The shared Reference Source MIT
license remains at `third_party/microsoft/referencesource/LICENSE.txt`.

One file differs from that tree:

| File | Change | Reason |
| --- | --- | --- |
| `ui/ScriptControlManager.cs` | qualify six `OrderedDictionary<,>` uses as `System.Web.Util.` | .NET 9 added `System.Collections.Generic.OrderedDictionary<TKey,TValue>`; both namespaces are imported, so the bare name is ambiguous. A `using` alias cannot name an open generic. |

## Generated Microsoft AJAX scripts

The tree carries script sources and build recipes, not the built scripts the
Framework assembly embeds. Thirteen `.jsa` recipes under `Script` drive a
preprocessor whose entire vocabulary is `#include "path"`, `#if SYMBOL`,
`#else`, `#endif`, and a `##SYMBOL <code>` line form. Every file is
self-balanced and nesting never exceeds one level; include paths are
backslash-separated and resolve against `Script`. Nine inputs carry a UTF-8 BOM
that a naive reader splices into the middle of the output.

`eng/GenerateAjaxScripts.cs` runs the recipes with `dotnet run` and writes
`src/Rehost.WebForms.Extensions/Scripts`. Release defines no symbol and debug
defines `DEBUG`, which yields the 26 names the assembly declares. `COPYRIGHT`
stays undefined in both: the shipped release script carries the single `//!`
banner from its `.jsa`, not the 62 per-file headers. `DEBUGINTERNAL` never
ships.

Output is committed rather than generated during the build, because the import
is a pinned snapshot. That diverges from
[generated build inputs](generated-build-inputs.json), which regenerates into
`obj`; the guard here is `AjaxScriptResourceTests`, which fails if a declared
name loses its resource or a directive survives generation.

The scripts are not minified, so they are three times the size of Microsoft's
and never byte-identical. Establishing what the release build did beyond
preprocessing needs a reading of a shipped 4.8.1 `System.Web.Extensions`, which
has not been taken.

`Rehost.WebForms.Extensions` compiles only the frozen-template closure. Excluded
public APIs are absent rather than stubbed. Design-time metadata the closure
still names is carried by internal shapes in the runtime's
`Compatibility/DesignTime`, which keeps the control declarations byte-for-byte;
see [dependency decisions](../dependency-decisions.md).
