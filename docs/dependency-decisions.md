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

## Security compatibility packages

`System.Security.Permissions` restores legacy type availability but not CAS
enforcement. Runtime remains full trust.

`System.Security.Cryptography.Xml` is directly pinned to a serviced version
because the MSBuild task dependency graph previously selected a vulnerable
transitive version. Audit every dependency change for advisories.

`System.Runtime.Serialization.Formatters` supports trusted legacy state/resource
compatibility only. It is not safe for untrusted payloads.
