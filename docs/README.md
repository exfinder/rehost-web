# Documentation

Read [`../PROJECT.md`](../PROJECT.md), then
[`../ROADMAP.md`](../ROADMAP.md).

## Current truth

- [Compatibility and evidence](compatibility.md)
- [Migrating an application](migration.md)
- [Unresolved work](backlog.md)
- [Reached portability edges](portability-ledger.md)
- [Shared terminology](../CONTEXT.md)

## Runtime contracts

- [Classic managed runtime model](classic-managed-runtime-model.md)
- [Application bootstrap and configuration](application-bootstrap-and-configuration.md)
- [AppDomain, remoting, and CAS boundary](appdomain-remoting-cas-compatibility.md)
- [Call-context boundary](call-context-compatibility.md)
- [Application Services assembly boundary](application-services-compatibility.md)
- [Dependency decisions](dependency-decisions.md)

## Focused compatibility

- [Extensions](extensions-compatibility.md) and
  [Web Services](web-services-compatibility.md)
- [Generated build inputs](generated-build-inputs.md),
  [generated resources](generated-resource-contracts.md), and
  [XSD build provider](xsd-build-provider-compatibility.md)
- [Data protection](data-protector-compatibility.md),
  [ResX](resx-reader-compatibility.md), and
  [SMTP](smtp-configuration-compatibility.md)
- [Enterprise Services](enterprise-services-compatibility.md),
  [remote configuration](remote-configuration-compatibility.md), and
  [Windows administration](windows-administration-compatibility.md)
- [Filesystem semantics](filesystem-semantics.md)

## Decisions and evidence

- [Current architecture decisions](adr/README.md)
- [Background research](research/README.md)
- [Source and transformation records](provenance/)
- [Framework configuration reference](framework-config-reference.md)
- [IIS configuration reference](iis-config-reference.md)

## Contributor workflow

- [Bringing up an application](bringing-up-an-application.md)
- [Code style](code-style.md)
- [Writing tests](writing-tests.md)
- [Scenario fixture tenancy](../tests/Rehost.WebForms.ScenarioHost/fixtures/README.md)
- [Parity rigs](../tests/parity/README.md)
- [Windows validation](windows-validation-host.md)
- Linux validation: `eng/linux-round.sh`
- Documentation checks: `python3 eng/check-docs.py`
- [Reusable Markdown audit prompt](prompts/markdown-documentation-audit.md)

Files under [`follow-ups/`](follow-ups/) contain unresolved design detail only;
[`backlog.md`](backlog.md) is the complete index and priority authority.
