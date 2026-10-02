# App and Host layout

## Decision

Keep an application library and executable Host in separate projects, with one
NuGet dependency graph. Stage Web Forms content under rehost_root and compile the
shared payload into its bin folder. The Host uses Microsoft.NET.Sdk.Web.

## Rationale

Legacy source needs its own language pin, disabled implicit usings/nullable and
original assembly identity. Combining it with top-level host code introduces
compiler-setting conflicts and ambiguous System.Web/ASP.NET Core names. Tests
and companion libraries can keep referencing the App DLL independently of Kestrel.

One graph prevents runtime dependency conflicts that isolated restores cannot
see. A separate binary layout adds resolver/deps-manifest and MSBuild task
complexity while moving few useful files; bin is not served and host namespaces
are not automatically imported by pages.

The project sets OutDir because package targets import after the SDK derives
binary/deps/runtimeconfig paths. Package props import before the project defines
its site root and cannot safely move every referencing project's output.

Host content root is the binary directory containing appsettings and Core assets;
Web Forms physical root is its parent. Both are derived before runtime activation
can change process base-directory state, so launch working directory is irrelevant.
The Web SDK supplies server GC; hosting targets suppress stage globs, transform
content copying and its generated ASP.NET Core Module web.config.

## Consequences

App and Host may migrate independently. ASP.NET Core coexistence needs a dispatch
mode and state bridges; split layout should return only when SDK support or a real
coexistence consumer justifies it. Remaining decisions are in
[migration stages](../follow-ups/migration-stages-and-site-layout.md).
