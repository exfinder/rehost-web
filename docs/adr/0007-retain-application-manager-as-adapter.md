---
status: accepted
---

# Retain ApplicationManager as a legacy hosting adapter

`ApplicationManager` remains initialized internally in a constrained
single-application mode. Rehost reuses its hosting-environment serialization,
well-known-object management, and activation/shutdown bookkeeping, while the
application runtime owns process lifecycle and request admission.

Child AppDomains, remoting, multi-application management, IIS preload/ping,
LRU recycle, and AppDomain unload are not portable `ApplicationManager`
responsibilities and fail explicitly when reached.
