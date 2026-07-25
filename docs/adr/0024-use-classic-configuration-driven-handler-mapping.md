---
status: accepted
---

# Use classic configuration-driven handler mapping

The first executable slice maps a request through the normal classic
`system.web/httpHandlers` configuration and `HttpApplication` handler-resolution
path to a precompiled handler type.

The host must not inject a handler instance or dispatch directly to it. This
preserves configuration matching, handler factories, module stages, and handler
reuse semantics while deferring dynamic `.aspx` compilation.
