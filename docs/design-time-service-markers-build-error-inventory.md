# .NET 10 inventory after designer-service markers

## Scope and decision

This pass adds internal compile-time shapes for `IWebApplication`,
`WebFormsRootDesigner`, and `WebFormsReferenceManager`. They satisfy legacy
designer-only branches while providing no instances or implementation.
Windows design-time functionality remains unsupported.

The shapes remain outside Reference Source and are a separately approved shim
group. They do not establish approval for other compatibility shims.

## Diagnostic delta

| Measure | UITypeEditor marker | Service markers | Delta |
| --- | ---: | ---: | ---: |
| Errors | 122 | 119 | **-3** |
| Warnings | 1,083 | 1,083 | 0 |
| All diagnostics | 1,205 | 1,202 | **-3** |
| Files with errors | 47 | 44 | -3 |
| Unique message groups | 104 | 103 | -1 |

The three removed errors were missing `System.Web.UI.Design` namespace imports
in `ExpressionBuilder`, `SkinBuilder`, and `TagNameToTypeMapper`. No further
errors or warnings appeared.

No Reference Source, source exclusion, warning policy, or runtime behavior
changed. Build: SDK 10.0.302 / MSBuild 18.6.11.33009, macOS arm64, exit 1,
2.63 seconds. Only the normalized summary is retained; temporary detailed
artifacts are not.
