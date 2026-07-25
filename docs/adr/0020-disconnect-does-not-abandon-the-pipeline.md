---
status: accepted
---

# A disconnect does not abandon the managed pipeline

The host adapter exposes client disconnect and host-stop signals through the
worker-request connection surfaces. It does not forcibly terminate System.Web
or stop awaiting while the pipeline may still access request state.

The first executable slice awaits normal `EndOfRequest` or an adapter terminal
failure. Forced request termination and timeouts are deferred.
