# Remote configuration compatibility

Remote IIS configuration is unsupported. Its imported host, server, stream,
and public server interface depend on .NET Remoting, Windows impersonation,
COM, IIS, and Windows ACLs; they remain untouched but are excluded from
compilation. Their public contracts are intentionally absent.

`WebConfigurationHost` still requires the internal host type for local
compilation. Its project-owned replacement throws `PlatformNotSupportedException`
at construction. Local configuration behavior is unaffected. The mandated
build moves 70 errors/2,441 warnings to 61 errors/2,431 warnings. Revisit only
if remote IIS administration becomes an explicit requirement.
