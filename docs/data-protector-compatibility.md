# DataProtector compatibility

`System.Security.Cryptography.DataProtector` is an internal System.Web
compatibility type, not a published replacement for the Framework contract.

The base implementation uses portable `SHA256.Create()` and
`CryptographicOperations.FixedTimeEquals`. It has no DPAPI or Windows runtime
dependency. Framework English error text is retained; localization is not
implemented.

The Framework public type is intentionally internal. External subclasses and
binary compatibility with `System.Security.dll` are unsupported.
`DpapiDataProtector` is not a portable default; existing DPAPI payloads require
explicit Windows migration and re-protection.

Implementation:
`src/Rehost.WebForms.Runtime/Compatibility/DataProtector.cs`.
Concrete provider and wire-format work:
[data protection provider](follow-ups/data-protection-provider.md).
