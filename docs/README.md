# Documentation

## Using Rehost.Web

- [Will my app work?](what-works.md): application types, tested features, and limits.
- [Getting started](getting-started.md): build and run your first application.
- [Running examples](../apps/README.md): choose an application to try locally.
- [Troubleshooting](troubleshooting.md): fix build and startup problems.
- [Build and publish](build-and-publish.md): manage output files and publish your app.
- [Migration guide](migration.md): a checklist for adapting an existing application.

## Developing Rehost.Web

Read [`../PROJECT.md`](../PROJECT.md), then
[`../ROADMAP.md`](../ROADMAP.md).

### Project references

- [Detailed compatibility reference](compatibility.md)
- [Migration reference](migration-reference.md)
- [Completed milestones](milestones.md)
- [Unresolved work](backlog.md)
- [Release notes](releases/0.1.0-alpha.1.md)
- [Reached portability edges](portability-ledger.md)
- [Shared terminology](../CONTEXT.md)

### Runtime contracts

- [Classic managed runtime model](classic-managed-runtime-model.md)
- [Application bootstrap and configuration](application-bootstrap-and-configuration.md)
- [AppDomain, remoting, and CAS boundary](appdomain-remoting-cas-compatibility.md)
- [Call-context boundary](call-context-compatibility.md)
- [Application Services assembly boundary](application-services-compatibility.md)
- [Dependency decisions](dependency-decisions.md)

### Focused compatibility

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

### Decisions and evidence

- [Current architecture decisions](adr/README.md)
- [Background research](research/README.md)
- [Source and transformation records](provenance/)
- [Framework configuration reference](framework-config-reference.md)
- [IIS configuration reference](iis-config-reference.md)

### Contributor workflow

- [Bringing up an application](bringing-up-an-application.md)
- [Architecture design principles](architecture-design-principles.md)
- [Code style](code-style.md)
- [Code comments](code-comments.md)
- [Writing tests](writing-tests.md)
- [Scenario fixture tenancy](../tests/Rehost.Web.ScenarioHost/fixtures/README.md)
- [Parity rigs](../tests/parity/README.md)
- [Cross-platform validation](cross-platform-validation.md)
- [Windows validation](windows-validation-host.md)
- Linux validation: `eng/linux-round.sh`
- Linux x64 validation: GitHub Actions, `.github/workflows/linux-x64.yml`
- Documentation checks: `python3 eng/check-docs.py`
- [Reusable Markdown audit prompt](prompts/markdown-documentation-audit.md)

Files under [`follow-ups/`](follow-ups/) contain unresolved design detail only;
[`backlog.md`](backlog.md) is the complete index and priority authority.
