# Cross-platform validation

A change validates with one round on the current platform.

Windows x64, Linux, and macOS arm64 must all pass before work is reported as
done when the change touches runtime logic that depends on the platform: paths
and separators, filesystem behavior (case sensitivity, hidden files, file
watching), native libraries, process and cross-process synchronization, or a
branch on the operating system. Defects so far have appeared on only one
platform: path separators, hidden-file classification, and native libraries
carrying the `.dll` extension off Unix.

A branch that exists to handle a platform difference must be exercised on the
platform that triggers it. A guard that has never executed is not a guard.

Case-sensitive-filesystem branches are triggered by a filesystem, not an OS:
the routine macOS suite exercises them on a disposable case-sensitive APFS
volume (`CaseSensitiveDirectory` fixtures; the same tests skip on
Windows/NTFS, which cannot express the situation).
A story touching them also runs one Linux round on the deployment-target OS: `eng/linux-round.sh`
(committed HEAD only, like a Windows round).

Cross-process synchronization uses a named `Mutex`. It is the only named
synchronization object supported on every target: named `EventWaitHandle` and
`Semaphore` throw `PlatformNotSupportedException` off Windows. Names carry the
`Local\` prefix, which is honored on both. A name derived from a string hash
must use a stable hash, since `string.GetHashCode` is randomized per process and
each process would otherwise take a different mutex.

That rule covers mutual exclusion. Signalling one process from another — an
arrival, a release, a payload — goes over a named pipe (`ScenarioGate`): a mutex
cannot tell its holder that someone is waiting, so the same handshake needs two
of them plus polling, and ownership is thread-affine.