---
status: accepted
---

# Preserve first-request initialization

The application runtime starts accepting requests after request-independent
hosting initialization and pre-application hooks succeed. Request-dependent
runtime initialization, `Global.asax` compilation, `Application_Start`, and the
first normal `HttpApplication` allocation remain single-flight work of the
first real request.

Eagerly simulating this work would change request context, user-code timing,
and classic managed-pipeline failure behavior.
