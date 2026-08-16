# Filesystem semantics

Framework's end-to-end case-insensitivity remains the contract on every
platform. A path the runtime produced that misses is resolved segment-by-segment
to the filesystem's real casing below the nearest existing directory — the
application root for anything mapped under it, otherwise the deepest ancestor
that exists; exact matches win. If a case-insensitive fallback finds multiple
names that differ only by case, the request fails naming the collision rather
than choosing nondeterministically; a directory that cannot be read leaves the
path unchanged. `Request.Path` retains the client's casing.

Windows-strict name rules remain portable application rules. Trailing dots or
spaces, embedded colons, and equivalent invalid shapes do not become supported
because a Unix filesystem accepts them. A file is hidden only when its
filesystem `Hidden` attribute says so; dot-prefixed names remain ordinary.

Portable separator, enumeration, stable-hash, and parent-walk repairs live in
the imported `FileUtil` path. Case canonicalization occurs once per path-producing
seam — `HostingEnvironment.MapPathActual`, `HttpRequest.PhysicalPathInternal`
for the path a worker request concatenates itself,
`UserMapPath.GetPhysicalPathForPath` for the configuration system, which maps
lowercased configuration paths and composes `web.config` itself, and the
server-include fallback that composes a physical path above the application
root — keeping downstream ignore-case compilation and configuration caches
coherent. Resolution is idempotent, so a path crossing two seams folds once.

## Physical or virtual

Framework's classifier, `UrlPath.IsAbsolutePhysicalPath`, knows two physical
shapes, `X:\` and `\\server\share` (either separator). Everything else — a
`/`-rooted string included — is virtual, at every path-taking site: server
includes, `<pages masterPageFile>`, site-map node urls, `MailDefinition`,
`XmlDataSource`, `Control.OpenFile`/`MapPathSecure`, `WriteFile`/`TransmitFile`,
`Server.MapPath` (IIS Express reading, ledger P71). The port keeps that
classifier untouched, so a Unix-rooted string is a virtual path here exactly as
`/x` is on Framework; what Unix cannot express is an absolute *physical* path in
those APIs, since `X:\` has no spelling there — an application passing one goes
through a virtual path instead. Only a caller that already holds a translated
physical path needs an explicit entry point past the classifier
(`TransmitFileTranslated`/`WriteFileTranslated`, ledger P54/P61); the
`FileChangesMonitor` alias checks will need the same once file-change
notification is enabled (configuration-reload follow-up).

Remaining work is indexed under path mapping and filesystem-related capability
rows in [the backlog](backlog.md) and [compatibility map](compatibility.md).

Evidence: ledger P23, P25, P32, P36, P54, P56, P57, P61, P70, and P71;
`FileUtilTests`, `CanonicalCasePathTests`, `CaseInsensitiveUrlOverKestrelTests`,
`CaseSensitiveDirectoryConfigOverKestrelTests`, `PathCasingOverKestrelTests`,
`PathClassificationOverKestrelTests`, `ServerIncludesOverKestrelTests`,
`StaticFilesOverKestrelTests`, and `ServerTransferOverKestrelTests`.
