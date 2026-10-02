# Extensions scope

The implemented profile is in [Extensions compatibility](../extensions-compatibility.md).

## Open contract

- Enabled JSON profile/authentication/role services over real providers. A fixture
  must isolate provider configuration from the shared host's other tenants;
  a real application enabling these services is the preferred trigger.
- Async error formatting under customErrors On. The existing body-customerrors
  fixture lacks page mappings; use an isolated page-capable configuration.
- LinqDataSource: decide whether a real application justifies a System.Data.Linq port.
- WCF proxy codegen for raw svcmap inputs and WCF-hosted application services:
  decide port/substitute scope when reached; JSON routes do not serve `.svc`.
- Debug script generation and localized satellites. Release preprocessing cannot
  synthesize Framework's validation calls and debug member transformations.
- Inline ServiceReference proxies and their endpoint scope.

Client Services remains outside the portable contract because it is a Windows
desktop stack. Completion requires an explicit support boundary and tests for
each newly supported route, without disturbing shared fixture configuration.
