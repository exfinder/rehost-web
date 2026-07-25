---
status: accepted
---

# Translate AppDomain shutdown to host notification

In the single-application portable host, an imported request to shut down or
restart the application AppDomain emits one idempotent terminal shutdown
notification to the application owner.

The ASP.NET Core adapter responds through
`IHostApplicationLifetime.StopApplication`. An external supervisor may replace
the process. Runtime code must neither call `Environment.Exit` nor attempt
in-process reinitialization.

The first executable slice must implement this notification leaf even though
graceful request draining and complete disposal remain a later lifecycle slice.
This prevents disabled `AppDomain.Unload` code from looping or silently ignoring
a terminal condition.
