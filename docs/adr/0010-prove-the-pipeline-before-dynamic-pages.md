---
status: accepted
---

# Prove the classic pipeline with a precompiled handler first

The first executable request uses Kestrel and a precompiled `IHttpHandler`
through the complete classic managed pipeline. A configured managed module and
event log verify activation, request context, application pooling, module
ordering, handler mapping, response output, and completion.

Dynamic `Global.asax`, `App_Code`, and `.aspx` compilation follow as separate
slices so pipeline failures are distinguishable from code-generation failures.
