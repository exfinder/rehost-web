# Windows administration compatibility

`Management/regiisutil.cs` remains untouched but is excluded from the Runtime.
It implements Windows-only `aspnet_regiis`, COM/MMC registration, IIS browser
capability installation, and key-container administration—not request runtime
behavior. Its public COM activation surface is intentionally absent. Revisit
only if Windows administration tooling enters scope.

`BrowserCapabilitiesCodeGenerator.cs` and `BrowserCapabilitiesCompiler.cs`
also remain untouched but are excluded. Built-in browser detection remains;
application-level `App_Browsers` compilation throws
`PlatformNotSupportedException` instead of being silently ignored. The public
browser installation generator contract is intentionally absent.
