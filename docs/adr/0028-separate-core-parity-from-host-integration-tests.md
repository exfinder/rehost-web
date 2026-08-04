---
status: accepted
---

# Separate core parity from host integration tests

> Note: the IIS classic-mode probe clause below was never exercised and is
> dead — IIS is outside the portability contract
> ([ADR 0045](0045-test-architecture-after-the-suite-refactoring.md)). The
> two-column split remains current.

Run the same fixture against .NET Framework 4.8.1 and the portable core through
equivalent recording `HttpWorkerRequest` implementations. Compare ordered
lifecycle events, status, headers, body, errors, and completion.

Test the Kestrel adapter separately for request translation, asynchronous
completion, disconnect propagation, and host lifecycle integration. Transport
differences must not obscure core semantic drift.

Use IIS classic-mode probes only for hosting behavior that the managed oracle
harness cannot reproduce.
