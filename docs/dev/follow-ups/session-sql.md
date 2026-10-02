# Session state: SQL mode

Split from [session state](session-state.md). The client uses shipped legacy
System.Data.SqlClient; its public identity is governed by
[dependency decisions](../dependency-decisions.md). Transport has no native-call
blocker; schema provisioning and multi-host behavior remain unresolved.

## Store contract

Microsoft's InstallSqlState.sql creates ASPState procedures with session/application
rows in tempdb; restarting SQL Server drops those rows and a startup procedure
recreates tables. InstallPersistSqlState.sql is a separate persisted variant.
The client appends Initial Catalog=ASPState and checks TempGetVersion/GetMajorVersion,
so an arbitrary replacement schema fails its version gate. Exclusive-acquire
procedures, lock cookies and expiry must keep their actual semantics.

Default to the tempdb flavor with allowCustomSqlDatabase=false; the persisted
variant remains unassessed. Specify Encrypt and TrustServerCertificate policy
against the provisioned server. Connections use process identity because portable
impersonation is unavailable.

## Provisioning and validation

Obtain the installation scripts from a Framework installation under
`Windows/Microsoft.NET/Framework64/v4.0.30319`; keep local copies in ignored
third_party storage. A fresh validation machine needs that provisioning route.
Choose a SQL Server instance accessible from every platform, including an emulated
or remote route where the required native architecture is unavailable.

- Two independent host processes share one ASPState database and cookie: a write
  through one must be read through the other.
- Concurrent requests for one session serialize through the exclusive lock cookie.
- Pass Windows x64, Linux and macOS arm64 before removing the SQLServer preflight
  refusal.

The agreed temporary infrastructure exception is to skip store-backed tests after
initial platform validation until automated provisioning exists. Skipped tests
execute nowhere and provide no regression gate; their reason must name the
required infrastructure so re-enabling is mechanical.

## Deferred interoperability

Live Framework/port sharing requires sequenced runs over one database and cookie,
not trace comparison of independent sessions. Captured serialized rows can test
format compatibility more cheaply, but cannot establish live cross-runtime locking
or expiry. This broader mixed-farm contract is separate from the two-port-host gate.

## Done when

SQLServer mode reaches the provisioned schema, two-host sharing and contention
pass across platforms, and provisioning plus test enablement have explicit owners.
