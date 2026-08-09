# Connection abort at request timeout

`executionTimeout` is currently cooperative: it cancels
`Request.TimedOutToken`, flags the request, and unwinds at the next pipeline-step
boundary. A synchronous step that never returns cannot be stopped safely on
modern .NET.

Bounding the client's wait requires the hosting layer to abort the connection at
the budget while leaving the server-side step to finish or remain stuck.
Framework used the same transport escape when timeout occurred during a
synchronous entity-body read.

## Required work

- Define the owner and timing of connection abort relative to response seal,
  final flush, and `RequestAborted`.
- Cover abort racing normal completion, error formatting, and host shutdown.
- Preserve the cooperative 500 when the step returns before transport abort.
- Ensure the server does not present a partially committed response as success.

## Done when

A never-returning synchronous step cannot hold the client connection beyond the
declared policy, without pretending the server-side work was terminated.
