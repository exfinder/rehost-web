---
status: accepted
---

# Do not offload System.Web request entry

The host adapter invokes `HttpRuntime.ProcessRequest` directly, then
asynchronously awaits the worker request’s completion signal. Wrapping request
entry in `Task.Run` would still consume a thread for synchronous legacy work
while adding scheduling and execution-context differences.
