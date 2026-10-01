# Windows administration compatibility

Windows administration tooling is outside runtime scope.

- `Management/regiisutil.cs` remains imported but excluded. Its
  `aspnet_regiis`, COM/MMC, IIS browser installation, and key-container
  contracts are absent.
- `BrowserCapabilitiesCodeGenerator` is public as on Framework, but its
  machine-wide members, `Create()` and `Uninstall()` (what `aspnet_regbrowsers`
  ran), throw `PlatformNotSupportedException` pointing at the application's
  `App_Browsers` folder. The strong-named `ASP.BrowserCapsFactory` assembly is
  never probed; the built-in factory is the base.
- Built-in browser detection and application `App_Browsers` compilation remain.

Revisit only if administration tooling becomes an explicit product.
