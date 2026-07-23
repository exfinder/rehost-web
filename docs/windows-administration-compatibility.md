# Windows administration compatibility

Windows administration tooling is outside runtime scope.

- `Management/regiisutil.cs` remains imported but excluded. Its
  `aspnet_regiis`, COM/MMC, IIS browser installation, and key-container
  contracts are absent.
- Imported browser administration/compiler sources remain excluded.
- Built-in browser detection remains.
- Application-level `App_Browsers` compilation throws
  `PlatformNotSupportedException`.

Revisit only if administration tooling becomes an explicit product.
