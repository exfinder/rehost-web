namespace Rehost.Web.AspNetCore.Tests;

// The structural reasons docs/dev/writing-tests.md accepts for a private host process. Closed on
// purpose: a new reason is a doc amendment plus a member here, never ad-hoc prose.
internal enum IsolationReason
{
    // The host itself is configured differently (Kestrel limits, a custom application root).
    HostConfiguration,

    // The claim is about activation, so the process must be cold when the request arrives.
    ColdActivation,

    // The tests hang, exhaust, or otherwise damage the host; nothing else may neighbor them.
    ProcessDamage,
}
