# Windows administration compatibility

`Management/regiisutil.cs` remains untouched but is excluded from the Runtime.
It implements Windows-only `aspnet_regiis`, COM/MMC registration, IIS browser
capability installation, and key-container administration—not request runtime
behavior. Its public COM activation surface is intentionally absent. Revisit
only if Windows administration tooling enters scope.
