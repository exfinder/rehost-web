---
status: accepted
---

# Port in vertical semantic slices

Port and prove the managed runtime in this order:

1. oracle harness and classic-path portability ledger;
2. bodyless precompiled-handler classic pipeline;
3. dynamic compilation substrate, pre-application start, `App_Code`,
   `Global.asax`, and `Application_Start`;
4. `.aspx` GET parsing, handler creation, page lifecycle, and rendering;
5. request-body bridging, forms, postback, view state, and uploads;
6. built-in modules and services such as session, cache, authentication,
   resources, and routing; and
7. graceful shutdown and broader transport features.

Each slice is gated before work proceeds to the next.
[ADR 0044](0044-gate-differentials-by-evidence-not-by-slice.md) decides whether
that gate is a Framework differential or port-local tests on both platforms.
