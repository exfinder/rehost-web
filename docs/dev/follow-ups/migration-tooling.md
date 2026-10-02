# Migration tooling

`dotnet new rehost-web` writes a WAP App/Host pair; it does not inspect the legacy
project. General automation must follow the [bring-up process](../bringing-up-an-application.md).

## Open contract

- Audit `packages.config` against Rehost package coverage and actual System.Web
  bindings. Include Windows-only APIs, physical backslashes and dependency assets.
- Inspect project customizations: post-build events, custom targets and content
  generators. Distinguish Web Sites from WAPs before choosing the project shape.
- Generate an App/Host pair, or Host-only Web Site, solution and package mapping.
  Seed XDT with required trust/target-framework fixes; keep application inputs intact.
- Select template packages from legacy references rather than always emitting the
  stock Web Forms set, which Web API and Web Pages consumers must remove manually.

## Done when

Scaffolding follows the audited project shape and dependency closure, reports
unsupported inputs and builds from packages outside this checkout.
