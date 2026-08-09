# WebFormsApplication

Frozen .NET Framework 4.8.1 Visual Studio Web Forms template application.
The legacy `packages.config` installs copied the tracked `Content/` and
`Scripts/` files into the application. Package restore recreates `packages/`,
not missing application content. Keep these assets committed; review package
upgrade additions and deletions as ordinary source changes.
