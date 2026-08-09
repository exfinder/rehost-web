# Configuration and compilation

This is the accepted target. Current bootstrap discovers default assets from
`AppContext.BaseDirectory` and preflights mapped sections; those gaps are
documented in [application bootstrap](../application-bootstrap-and-configuration.md)
and the [backlog](../backlog.md).

## Decision

The host supplies versioned machine, root-web, and IIS-baseline assets by
explicit absolute path. System.Configuration retains parsing, inheritance,
validation, caching, and failure timing. Registration validates only host-owned
inputs; it does not preflight mapped System.Web sections before retained hosting
initialization consumes them.

Root web configuration is structurally derived from Framework 4.8.1. Application
configuration may amend it but is never silently rewritten. Unsupported or
conflicting settings fail at the retained consumption boundary.

Keep configuration-driven handlers/modules and `BuildManager` initialization.
Dynamic C# pages compile in process through the configured Roslyn provider;
generated assemblies and application `bin` load into the default context.

## Consequences

- Missing application `web.config` means baseline inheritance.
- Configuration remains immutable for the process generation.
- Application fixtures may clear inherited registrations for isolation; that
  does not redefine product defaults.
- C# provider details are in [Roslyn page compilation](0007-roslyn-page-compilation.md).
- Codegen location/reuse details are in [codegen storage](0008-codegen-storage.md).
- Visual Basic and unavailable build-provider assemblies fail explicitly when
  reached.
