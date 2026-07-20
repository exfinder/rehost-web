# SMTP configuration contract

Restored the public `System.Net.Configuration` SMTP configuration surface used
by `RuntimeConfig` and `MailDefinition`:

- `SmtpSection`
- `SmtpNetworkElement`
- `SmtpSpecifiedPickupDirectoryElement`

Source: local Microsoft Reference Source clone,
`System/net/System/Net/Configuration`, corresponding files. Rehost keeps the
public properties, defaults, validators, and converters. Framework-internal
mail snapshots and the legacy CAS unrestricted-port demand were omitted: they
are unused by System.Web and unsuitable for the cross-platform runtime.

Validation: focused xUnit v3 + Shouldly suite passes 3/3; mandated runtime
build moves 28 errors to 27 with warnings unchanged at 1,083. Remaining errors
are excluded remoting, EnterpriseServices, COM, XSD, and serialization groups.
