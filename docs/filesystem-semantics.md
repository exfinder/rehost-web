# Filesystem semantics

Framework's end-to-end case-insensitivity remains the contract on every
platform. Virtual-to-physical mapping resolves a miss segment-by-segment to the
filesystem's real casing; exact matches win. If a case-insensitive fallback
finds multiple names that differ only by case, the request fails naming the
collision rather than choosing nondeterministically. `Request.Path` retains the
client's casing.

Windows-strict name rules remain portable application rules. Trailing dots or
spaces, embedded colons, and equivalent invalid shapes do not become supported
because a Unix filesystem accepts them. A file is hidden only when its
filesystem `Hidden` attribute says so; dot-prefixed names remain ordinary.

Portable separator, enumeration, stable-hash, and parent-walk repairs live in
the imported `FileUtil` path. Case canonicalization occurs once per path-producing
seam — `HostingEnvironment.MapPathActual`, and `HttpRequest.PhysicalPathInternal`
for the path a worker request concatenates itself — keeping downstream ignore-case
compilation and configuration caches coherent. Resolution is idempotent, so a path
crossing both seams folds once.

A rooted Unix path cannot reveal whether the caller meant a physical or virtual
path. Call sites that know they hold a translated physical path use an explicit
entry point. Public Framework APIs keep their original classifier until a real
application supplies evidence for a different contract.

Remaining work is indexed under path mapping and filesystem-related capability
rows in [the backlog](backlog.md) and [compatibility map](compatibility.md).

Evidence: ledger P23, P25, P32, P36, P54, P56, P57, and P61;
`FileUtilTests`, `CanonicalCasePathTests`, `CaseInsensitiveUrlOverKestrelTests`,
`StaticFilesOverKestrelTests`, and `ServerTransferOverKestrelTests`.
