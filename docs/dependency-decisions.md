# Dependency decisions

Package versions are canonical in `Directory.Packages.props`; project usage is
canonical in each `.csproj`.

## System.Data.SqlClient

Runtime public APIs expose concrete legacy `System.Data.SqlClient` types.
`Microsoft.Data.SqlClient` cannot transparently replace them. Both providers
may coexist, but provider identity or adapter changes require an ADR and
differential tests.

## CodeDOM and MSBuild

System.Web runtime compilation uses `System.CodeDom` and executes MSBuild task
contracts. These packages are runtime architecture dependencies, not only build
tools. Toolset selection, deployment, language providers, and precompilation
remain separate compatibility decisions.

## Roslyn

`Microsoft.CodeAnalysis.CSharp` compiles generated pages in process, because
`System.CodeDom` cannot: its providers throw `PlatformNotSupportedException`.
See [ADR 0041](adr/0041-compile-pages-through-a-configured-roslyn-provider.md).

The dependency is deployed, not merely built against, and costs roughly 10 MB.
Nothing loads it until a compilation is requested. Compiling out of process
would instead require shipping a compiler toolset, discovering an executable at
runtime, and owning compiler-server lifetime.

The pinned version determines the language surface an application compiles
against whenever `compilerOptions` does not specify `/langversion:`, so bumping
it is a compatibility change, not only a servicing one.

## Security compatibility packages

`System.Security.Permissions` restores legacy type availability but not CAS
enforcement. Runtime remains full trust.

`System.Security.Cryptography.Xml` is directly pinned to a serviced version
because the MSBuild task dependency graph previously selected a vulnerable
transitive version. Audit every dependency change for advisories.

`System.Runtime.Serialization.Formatters` restores `BinaryFormatter`, which .NET 9
removed. Microsoft ships it unsupported; the in-box implementation throws, and no
feature switch revives it. Web Forms reaches `BinaryFormatter` from view state
(`ObjectStateFormatter`), out-of-process session state, the roles cookie,
out-of-process output cache, preserved compilation results, `LosFormatter`, and
binary-serialized `.resx` nodes. Several of those carry untrusted input, so the
port's deserialization exposure equals .NET Framework's — by design, since the
port exists to run existing applications unchanged. It is neither safer nor less
safe than the framework it replaces, and it does not narrow that surface.

`ExcludeAssets="compile"` keeps the typerefs on the in-box 8.1.0.0 reference while
the package supplies the 10.0.0.0 implementation at run time.

Two mechanisms turn the runtime switch on, and neither subsumes the other. The
shipped `build/Rehost.WebForms.Runtime.targets` writes it into the consuming
application's runtimeconfig, which is correct from process start and independent
of load order, but reaches only consumers that take the package or import the
targets. Every executable in this repository that consumes the port imports them,
so the test and prototype processes run the shape that ships.
`UnsafeBinaryFormatterModuleInitializer` covers a `ProjectReference` consumer that
imports neither. Its limit is that `BinaryFormatter` caches the
switch on first read: a process whose host code serialized before it first
touched System.Web keeps the SDK default of `false`, and the initializer, though
it runs and does set the switch, cannot undo that. Measured both ways.
