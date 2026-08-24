# Request completion, failure, and cancellation

The adapter now seals and completes exactly once at `EndOfRequest`; a dispatcher
escape faults completion; System.Web-owned failures complete after managed
formatting; disconnect remains observable without abandoning a pipeline-owned
request. Unit tests cover duplicate completion, sealed output, body abort, and
transport failure.

## Open

- Add full-pipeline cases for escape before pipeline ownership, asynchronous
  callback failure, cancellation during owned work, and final-commit failure.
- Prove every terminal branch completes or faults the middleware task without
  replacing the original exception.

## Verification

Keep adapter unit tests for ownership mechanics; use Kestrel integration tests
for the remaining terminal-event matrix.

## Done when

No request can wait indefinitely after a terminal event, an escaped exception
is observed exactly once, and managed completion happens exactly once.
