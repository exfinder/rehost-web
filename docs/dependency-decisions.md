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

`System.Runtime.Serialization.Formatters` supports trusted legacy state/resource
compatibility only. It is not safe for untrusted payloads.
