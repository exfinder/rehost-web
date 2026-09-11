# Rehost WebForms

Run predominantly managed ASP.NET Web Forms applications on modern .NET across
Windows, Linux, and macOS with minimal application-source changes.

The `Rehost.WebForms` package is the starting point: it carries the
`System.Web`-compatible runtime and the companion assemblies the root
configuration names, so `asp:` tags parse out of the box. Packages that were
NuGet packages on .NET Framework (`FriendlyUrls`, `Optimization`,
`ScriptManager`) map one-to-one to their `Rehost.*` counterparts — translate
your `packages.config` line by line. A host executable references
`Rehost.WebForms.Hosting` and serves the site through the classic managed
pipeline on Kestrel.

The legacy application folder is never modified and stays buildable on
.NET Framework side by side.

See the repository for documentation, compatibility claims, and samples.
