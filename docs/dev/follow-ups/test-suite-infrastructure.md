# Test-suite infrastructure

## Open contract

- Routine tests need a three-OS CI matrix; Framework oracle work is exceptional.
- Decide runner configuration for parallelism and hung-test detection rather than
  inheriting xUnit defaults without review.
- Set a suite timing budget and decide where periodic mutation checks belong.
- Reduce real-clock waits in timeout fixtures only if the sweep contract permits it.

## Done when

The ordinary suite runs across the required platforms with explicit failure and
hang handling; infrastructure changes exercise actual scenarios.
