# Windows host CPU throttling over SSH

Anything launched over SSH has no foreground presence, so Windows assigns it
**EcoQoS**. Two separate penalties follow, and they need separate fixes:

- threads are confined to E-cores — measured at exactly 0.0% P-core share, not a
  preference;
- core frequency is clamped, on both core types.

Measured on `winbox`, full suite, 163 tests:

| | time | failures |
| --- | --- | --- |
| untreated | 61–69s | 8–11 |
| machine-wide registry setting only | 25.5–26.3s | 0 |
| registry setting + per-process exemption | 14.9–15.2s | 0 |

An interactive desktop session runs the same suite in about 15s, so the treated
SSH path is no longer the slower one.

## The frequency clamp: a machine-wide registry setting

`PowerThrottlingOff=1` under
`HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling` disables the
frequency clamp machine-wide. It needs an elevated merge and a reboot.

**This is host state, not repo state.** It is already applied to `winbox`. A
different Windows host without it is roughly 2.5x slower with nothing in the
repository to explain why. `power-throttling-off.reg` and
`power-throttling-default.reg` sit in `C:\Users\sshuser\`.

## The E-core confinement: a per-process exemption

The registry setting does not affect placement — threads stay on E-cores. That
needs a per-process exemption, and the exemption attribute is **not inherited by
child processes**, so every descendant has to be caught individually after it
starts. `eng/Invoke-Unthrottled.ps1` polls the process tree and does this; a
suite run exempts about 40 processes, mostly scenario hosts.

```powershell
pwsh -NoProfile -File eng\Invoke-Unthrottled.ps1 -Command 'dotnet test Rehost.WebForms.slnx --no-build'
```

A copy lives at `C:\Users\sshuser\bin\Invoke-Unthrottled.ps1` so the wrapper
survives a checkout reset. It exits with the wrapped command's exit code.

From a one-shot `ssh` call, append `; exit $LASTEXITCODE`:

```bash
ssh winbox 'pwsh -NoProfile -File C:\Users\sshuser\bin\Invoke-Unthrottled.ps1 -WorkingDirectory C:\Users\sshuser\source\repos\rehost-webforms -Command "dotnet test Rehost.WebForms.slnx --no-build"; exit $LASTEXITCODE'
```

**The suffix is not optional.** The `pwsh` that sshd invokes reports its own
success or failure rather than the child's, so without it a failing round comes
back as exit 1 whatever the real code was — exit 7 was observed arriving as 1.
A round batched into one ssh call would then read as green.

## What does not work

Do not reach for CPU affinity or priority class. Both were measured and neither
works: affinity is inherited but strictly loses, because pinning to P-cores gives
up the 16 E-cores that the registry setting has already restored to full clock;
priority class made no measurable difference; CPU sets are not inherited at all.
The only mechanism that moves the number is the per-process exemption.

Nor is this SSH server configuration. Win32-OpenSSH exposes no relevant knob, and
the QoS assignment is not its doing — the scheduler applies it to any process
without foreground presence.

## Reading measurements

The first suite run after the host has been idle takes roughly twice as long as
the steady state — 34.6s against 15s, treated. Do not read a single cold
measurement as a regression; run it twice.

Load-sensitive test failures disappeared once the host stopped being slow, but
the tests that failed use fixed timeouts and will fail again on any sufficiently
contended machine. Treat the failures as latent rather than fixed.
