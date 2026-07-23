# SMTP configuration compatibility

System.Web restores the public `System.Net.Configuration` SMTP surface used by
`RuntimeConfig` and `MailDefinition`:

- `SmtpSection`;
- `SmtpNetworkElement`;
- `SmtpSpecifiedPickupDirectoryElement`.

Properties, defaults, validators, and converters derive from Microsoft
Reference Source `System/net/System/Net/Configuration`. Framework-internal mail
snapshots and the legacy CAS unrestricted-port demand are omitted because
System.Web does not use them and modern .NET cannot enforce CAS.

Implementation:
`src/Rehost.WebForms.Runtime/Compatibility/Configuration/SmtpSection.cs`.
Tests: `tests/Rehost.WebForms.Runtime.Tests/SmtpSectionTests.cs`.
