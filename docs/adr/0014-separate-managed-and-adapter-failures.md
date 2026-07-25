---
status: accepted
---

# Separate managed request failures from adapter failures

Application, module, and handler exceptions remain owned by System.Web: it
formats the response and ends the request. Host translation, transport, or
pre-pipeline entry failures are owned by the adapter and fault host-side
completion.

The worker request exposes one exactly-once terminal completion. If
`HttpRuntime.ProcessRequest` throws before System.Web can call `EndOfRequest`,
the adapter completes that request exceptionally so middleware cannot wait
forever. This alone does not decide whether the whole application is unusable.
