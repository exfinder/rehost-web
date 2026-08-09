# Documentation

Read [`../PROJECT.md`](../PROJECT.md), then
[`../ROADMAP.md`](../ROADMAP.md).

## Current truth

- [Compatibility and evidence](compatibility.md)
- [Unresolved work](backlog.md)
- [Reached portability edges](portability-ledger.md)

## Runtime contracts

- [Classic managed runtime model](classic-managed-runtime-model.md)
- [Application bootstrap and configuration](application-bootstrap-and-configuration.md)
- [AppDomain, remoting, and CAS boundary](appdomain-remoting-cas-compatibility.md)
- [Call-context boundary](call-context-compatibility.md)
- [Application Services assembly boundary](application-services-compatibility.md)
- [Dependency decisions](dependency-decisions.md)

Narrow compatibility rationale for generated inputs, resources, configuration,
Web Services, administration, and excluded build providers remains in the other
top-level documents in this directory.

## Decisions and evidence

- [Current architecture decisions](adr/README.md)
- [Background research](research/)
- [Source and transformation records](provenance/)
- [Framework configuration reference](framework-config-reference.md)
- [IIS configuration reference](iis-config-reference.md)

## Contributor workflow

- [Writing tests](writing-tests.md)
- [Scenario fixture tenancy](../tests/Rehost.WebForms.ScenarioHost/fixtures/README.md)
- [Parity rigs](../tests/parity/README.md)
- [Windows validation](windows-validation-host.md)
- Linux x64 validation: `DOCKER_DEFAULT_PLATFORM=linux/amd64 eng/linux-round.sh`
- Documentation checks: `python3 eng/check-docs.py`

Files under [`follow-ups/`](follow-ups/) contain unresolved design detail only;
[`backlog.md`](backlog.md) is the complete index and priority authority.
