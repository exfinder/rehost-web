# Package license decision

The packages carry nuget.org-ready metadata (readme, repository URL,
SourceLink, snupkg symbols, shared version) but `PackageLicenseExpression` is
deliberately unset, and nuget.org publishing is blocked until it is chosen.

The decision is not only picking a license for this repository's own code: the
packages embed imported source, and the expression must be compatible with
every imported tree recorded under [`docs/provenance`](../provenance/):
Microsoft Reference Source (MIT per the pinned revision's LICENSE.txt),
AspNetWebOptimization (license text preserved at
`third_party/aspnet/AspNetWebOptimization/LICENSE.txt`), and any tree imported
later. The repository itself also has no top-level LICENSE file yet; that
lands with the same decision.

Until then, local feeds (`1.0.0-local`) are the only distribution channel.
