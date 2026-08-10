# System.Web.Extensions source

The import authority is the sibling `../referencesource` checkout at revision
`ec9fa9ae770d522a5b5f0607898044b7478574a3`. Its `System.Web.Extensions` tree
object is `4980d036e77731ef47248b6c5d849acb5b69cefe`; all 368 files are copied to
`src/System.Web.Extensions.ReferenceSource`. The shared Reference Source MIT
license remains at `third_party/microsoft/referencesource/LICENSE.txt`.

Two files differ from that tree:

| File | Change | Reason |
| --- | --- | --- |
| `ui/ScriptControlManager.cs` | qualify six `OrderedDictionary<,>` uses as `System.Web.Util.` | .NET 9 added `System.Collections.Generic.OrderedDictionary<TKey,TValue>`; both namespaces are imported, so the bare name is ambiguous. A `using` alias cannot name an open generic. |
| `ui/WebResourceUtil.cs` | `First()` → `FirstOrDefault()` when sizing the resource-name table | This assembly declares no `WebResourceAttribute`: scripts are served from physical paths and CDN through `ScriptResourceMapping`. Upstream throws `Sequence contains no elements` from the static initializer, replacing the diagnostic that names the missing resource. |

`Rehost.WebForms.Extensions` compiles only the frozen-template closure. Excluded
public APIs are absent rather than stubbed. Design-time metadata the closure
still names is carried by internal shapes in the runtime's
`Compatibility/DesignTime`, which keeps the control declarations byte-for-byte;
see [dependency decisions](../dependency-decisions.md).
