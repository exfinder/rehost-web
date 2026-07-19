# .NET 10 inventory after Cryptography.Xml security pin

## Scope and result

This security follow-up adds a centrally managed direct runtime reference to
`System.Security.Cryptography.Xml` 10.0.10, overriding the vulnerable 10.0.4
version required transitively by `Microsoft.Build.Tasks.Core` 18.8.2.

The package graph resolves 10.0.10. Both high-severity `NU1903` advisories and
all four repeated build-output occurrences disappeared.

| Measure | MSBuild Tasks pass | Security-pin pass | Delta |
| --- | ---: | ---: | ---: |
| Errors | 286 | 286 | 0 |
| Source warnings | 1,083 | 1,083 | 0 |
| NuGet audit-warning occurrences | 4 | 0 | **-4** |
| Normalized source diagnostics | 1,369 | 1,369 | 0 |

No Reference Source, cryptographic behavior, shim, source inclusion, feature
constant, or warning policy changed. Build: SDK 10.0.302 / MSBuild
18.6.11.33009, macOS arm64, exit 1, 3.96 seconds. Only the normalized summary
is retained; temporary detailed artifacts are not.
