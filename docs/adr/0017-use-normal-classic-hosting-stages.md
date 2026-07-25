---
status: accepted
---

# Use normal classic hosting stages

Portable activation does not set `HideFromAppManager`,
`ThrowHostingInitErrors`, or `DontCallAppInitialize`. Retained
`ApplicationManager` bookkeeping remains active, eligible initialization
failures remain available to System.Web request-error formatting, and
`App_Code.AppInitialize` retains its Framework placement.

File watching is disabled through `FcnMode.Disabled`; its ACL-reading
optimization may also be disabled because no watcher is created.
