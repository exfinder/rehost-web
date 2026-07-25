---
status: accepted
---

# Classify failures by ownership and mutation

Do not retain `fcc0372`'s catch-all bootstrap `Faulted` policy.

- Host-registration validation fails before global mutation. No application is
  published, and a corrected creation attempt is safe.
- An activation exception that escapes after global mutation begins terminates
  the generation, emits terminal shutdown notification, and is not retried.
- `HostingInit` and `FirstRequestInit` retain their cached System.Web
  initialization-error response before shutdown notification.
- `Application_Start`, module, and handler errors remain managed request
  failures unless Framework code itself requests shutdown.

The oracle must pin uncertain exception timing; naming or apparent startup
proximity does not determine failure scope.
