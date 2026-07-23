# Remote configuration compatibility

Remote IIS configuration is unsupported. Its Framework contracts require
remoting, Windows impersonation, COM, IIS, and Windows ACLs. Imported
host/server/stream sources remain preserved but excluded; their public
contracts are absent.

Local configuration remains supported. Its internal remote-host dependency is
replaced by
`src/Rehost.WebForms.Runtime/Compatibility/Configuration/RemoteWebConfigurationHost.cs`,
which throws `PlatformNotSupportedException` at construction.

Revisit only if remote IIS administration becomes an explicit product
requirement.
