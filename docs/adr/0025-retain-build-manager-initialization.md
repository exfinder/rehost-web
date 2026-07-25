---
status: accepted
---

# Retain BuildManager initialization

Preserve the Framework sequence from `HttpRuntime.HostingInit` through
`BuildManager.InitializeBuildManager` and `HttpApplicationFactory`.

The precompiled-handler slice uses existing configuration and type-resolution
machinery. Dynamic source compilation remains a later slice. Until implemented,
reaching a dynamic compilation leaf fails explicitly there; bootstrap must not
bypass or remove `BuildManager`.
