---
status: accepted
---

# Use immutable application generations

Configuration, binaries, `App_Code`, pages, resources, and generated state are
immutable after application activation. File-change notifications remain
disabled; deployment changes require a replacement process representing a new
application generation.

Rehost does not attempt manual cache clearing, partial configuration reload, or
in-process reconstruction of Framework static state.
