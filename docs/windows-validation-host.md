# Windows validation host

Windows x64 validation runs on `sshuser@192.168.1.7` (ssh alias `winbox`, defined
in `~/.ssh/config` with ControlMaster/ControlPersist so connections are reused),
in a dedicated persistent clone at `C:\Users\sshuser\source\repos\rehost-webforms`.
The clone may be reset freely. `winbox` is LAN-only; see the EC2 alternative
below when it is unreachable.

Cross-platform validation requires macOS arm64, Linux, and Windows x64 to
pass. Linux uses [`eng/linux-round.sh`](../eng/linux-round.sh), which runs the
container on the Docker daemon's native architecture — an emulated round costs
minutes where a native one costs seconds.
Defects found so far — path separators, hidden-file classification, native
libraries keeping the `.dll` extension off Unix — have each appeared on only one
platform. See the cross-platform validation policy in `AGENTS.md`.

## EC2 alternative: `win-oracle`

Reachable off the LAN. Windows Server 2025 Core, same toolchain, managed by
[`eng/win-oracle.sh`](../eng/win-oracle.sh); run it with no arguments for the
subcommands and tunables.

`ssh win-oracle` wakes a stopped instance through a `ProxyCommand`, so no `start`
is needed. It stops itself after an idle hour, so expect a ~30s first connection.
Prefer `winbox` on the LAN — it is faster and always on.

Ingress is pinned to one address; from a new network run `win-oracle.sh allow-ip`.

## Sync workflow

Push straight to the host over the LAN — no GitHub round-trip, no `git bundle`,
no `scp`. Batch the whole remote round (checkout, build, test) into one ssh call;
each round trip costs roughly 0.5s.

`git push -f` is blocked by the permission classifier. Push to a fresh ref name
each round instead, and delete it on the remote at the end of the same ssh call:

```bash
git add -A                       # separate call — do not combine with the push
REF=$(git commit-tree $(git write-tree) -p HEAD -m wip)
git push "winbox:C:/Users/sshuser/source/repos/rehost-webforms" \
  "${REF}:refs/heads/wip/win-<topic>"     # quote it: zsh eats $REF:refs as a :r modifier
# remote, one call: git checkout -f -B wintest wip/win-<topic>; git clean -fd;
#                   build; test; git branch -D wip/win-<topic>
```

`commit-tree` publishes the index without creating a local commit or moving
`HEAD`, so a Windows validation round never forces a premature commit on the
working checkout.

Do not put `git add -A` in the same compound command as the push: if the
permission classifier denies the compound, the `add` never runs and
`write-tree` silently publishes a stale index — a tree missing files can pass
validation against the wrong source.

Leave the remote checkout on `wintest` between rounds; do not restore it to
`main`. Remote `main` drifts behind and is never used by this workflow.

`checkout -f` leaves unrelated untracked files from earlier rounds. Run
`git clean -fd` immediately after checkout so the tested tree matches the pushed
tree. Do not add `-x`: ignored `obj/`, `bin/`, and package caches must survive.

## Build cache

Never `git clean -xdf` on the host. Measured 88s cold vs. 6.8s warm — keeping
`obj/`, `bin/`, and the NuGet cache between rounds is the entire speed win.

Run the remote build and test through `eng/Invoke-Unthrottled.ps1`, or the round
takes about 60s instead of 15s. Windows throttles SSH-launched processes; the
mechanism and the host setup it depends on are in
[windows-host-cpu-throttling.md](windows-host-cpu-throttling.md).

A failed remote build leaves the previous binaries in place, and
`dotnet test --no-build` then runs the stale assemblies and reports a passing
suite at the old test count. Always print the build result and compare the test
total against the macOS run before trusting a green Windows round. A dropped ssh
connection surfaces as build errors, not as a connection error — retry the build
before investigating the code.

## Auth and shell

`origin` is `git@github.com:exfinder/rehost-webforms.git` over SSH keys and
works non-interactively. HTTPS does not — the `wincredman` credential store
needs an interactive desktop session.

Drive PowerShell Core with `pwsh -NoProfile -EncodedCommand <base64 UTF-16LE>`;
plain quoting is mangled by the ssh shell before `pwsh` sees it. `pwsh` is Core
7.6.3 (not `powershell.exe` 5.1); global.json pins SDK 10.0.302 as a floor and
rolls forward within 10.0.

The scp-style URL matters for the push: `ssh://host/C:/...` fails to parse,
`host:C:/...` works.

`sshd` is configured with `pwsh` as its `DefaultShell`, so `ssh winbox` lands in
PowerShell Core directly and needs no wrapper shell.

## Exit codes over ssh

End every one-shot `ssh` command with `; exit $LASTEXITCODE`:

```bash
ssh winbox 'dotnet test Rehost.WebForms.slnx --no-build; exit $LASTEXITCODE'
```

The `pwsh` that sshd invokes reports its own success or failure, not the wrapped
command's, so without the suffix a failing round arrives as exit 1 whatever the
real code was — exit 7 was observed arriving as 1. Since a round is batched into
a single ssh call, that turns a real failure into an ambiguous one, and an
ambiguous one is easy to read as a dropped connection.

Use single quotes on the macOS side so the local shell leaves `$LASTEXITCODE`
alone, and double quotes for any nested argument.

## Framework binary readings

Published reference source lags shipped binaries: the 4.8.9319.0 cookie-
defaults servicing fix exists in no published branch, and the newest branch
(`NetFramework48ZDP`) carries only part of the SameSite servicing. When a
reading of real 4.8.1 behavior is needed, decompile the GAC assembly with
`ilspycmd` on any host carrying real 4.8.1 (`dotnet tool install -g
ilspycmd`; already installed globally on `win-oracle`):

```text
ilspycmd -t System.Web.HttpCookie `
  C:\Windows\Microsoft.NET\assembly\GAC_64\System.Web\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Web.dll
```

Prefer this over reflection plus raw IL: it yields reviewable C# for the
story doc, and it is how the `EnsureCookieDefaults` port was verified.
Reflection remains the right tool for reading live values (app settings,
registry-backed switches) rather than code shape.

## Framework wire readings

The in-proc readings rig (real `System.Web` driven through
`ApplicationManager` + `SimpleWorkerRequest`) observes the System.Web→server
seam. When the question is what a *client* received — framing
(`Content-Length` vs chunked), header presence on the wire, anything IIS
itself decides — use the wire rig on `win-oracle`: a minimal Web Forms app
under real IIS 10 (integrated pipeline, CLR v4.0), read by a raw socket so no
client convenience layer rewrites the answer.

The rig's sources are committed at [`eng/wire-rig`](../eng/wire-rig)
(`win-oracle` is rebuilt from scratch, so nothing on the box is durable);
`scp -r eng/wire-rig win-oracle:C:/readings/` restores it. On the box, at
`C:\readings\wire-rig`:
`app\` (probe pages + `web.config`, `debug="false"`), `setup.ps1` (installs
the `Web-Server`/`Web-Asp-Net45` features if absent and creates the `WireRig`
IIS site on port 8099), `read.ps1 -Path '/page.aspx?...'` (raw capture,
printed ISO-8859-1). Add stimuli by editing the probe pages and re-reading;
record the result near the consuming contract or portability-ledger row.

Probe before coding an assumption about IIS: readings have repeatedly
contradicted beliefs already written down — `remove`-of-absent is tolerated
where a draft had coded it strict (C2), and IIS omits `Last-Modified` on
304s where the obvious shape re-sends it.
