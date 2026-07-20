# Remote configuration compatibility

Remote IIS configuration is unsupported. The imported Reference Source stream
depends on .NET Remoting, Windows impersonation, COM, and IIS; it remains
untouched but is excluded from compilation. A project-owned stream preserves
the internal call shape and throws `PlatformNotSupportedException` at
construction.

Local configuration behavior is unaffected. xUnit v3 + Shouldly verifies the
explicit failure. The mandated build moves 6 errors/1,084 warnings to 4
errors/1,081 warnings. Revisit only if remote IIS administration becomes an
explicit requirement.
