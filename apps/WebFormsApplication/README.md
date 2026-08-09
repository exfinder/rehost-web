# WebFormsApplication

Frozen .NET Framework 4.8.1 Visual Studio Web Forms template application.
The legacy `packages.config` installs copied the tracked `Content/` and
`Scripts/` files into the application. Package restore recreates `packages/`,
not missing application content. Keep these assets committed; review package
upgrade additions and deletions as ordinary source changes.

Run `dotnet msbuild apps/WebFormsApplication/build.proj` from the repository
root. The driver incrementally produces ignored local packages, then builds the
SDK-style WAP sidecar through package references only. This build remains
expected to fail until the reached managed `System.Web` relatives are ported.
