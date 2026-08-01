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
| registry setting + per-process High QoS | 14.9–15.2s | 0 |

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

## The E-core confinement: pinning each process at High QoS

The registry setting does not affect placement — threads stay on E-cores. That
needs each process pinned at High QoS, and the attribute is **not inherited by
child processes**, so every descendant has to be caught individually after it
starts. `eng/Invoke-Unthrottled.ps1` polls the process tree and does this; a
suite run pins about 40 processes, mostly scenario hosts.

Windows names the levels High, Medium, Low, Eco, Multimedia and Deadline. The
policy being disabled is execution speed throttling, hence the API constant
`PROCESS_POWER_THROTTLING_EXECUTION_SPEED`:

| ControlMask | StateMask | result |
| --- | --- | --- |
| `EXECUTION_SPEED` | `EXECUTION_SPEED` | forced EcoQoS |
| `EXECUTION_SPEED` | `0` | pinned at High QoS — what the wrapper sets |
| `0` | `0` | system-managed, the throttled default over SSH |

```powershell
pwsh -NoProfile -File eng\Invoke-Unthrottled.ps1 -Command 'dotnet test Rehost.WebForms.slnx --no-build'
```

A copy lives at `C:\Users\sshuser\bin\Invoke-Unthrottled.ps1` so the wrapper
survives a checkout reset. It exits with the wrapped command's exit code.

From a one-shot `ssh` call the trailing `; exit $LASTEXITCODE` is required, for
the reason given in
[windows-validation-host.md](windows-validation-host.md#exit-codes-over-ssh):

```bash
ssh winbox 'pwsh -NoProfile -File C:\Users\sshuser\bin\Invoke-Unthrottled.ps1 -WorkingDirectory C:\Users\sshuser\source\repos\rehost-webforms -Command "dotnet test Rehost.WebForms.slnx --no-build"; exit $LASTEXITCODE'
```

## What does not work

Do not reach for CPU affinity or priority class. Both were measured and neither
works: affinity is inherited but strictly loses, because pinning to P-cores gives
up the 16 E-cores that the registry setting has already restored to full clock;
priority class made no measurable difference; CPU sets are not inherited at all.
The only mechanism that moves the number is pinning each process at High QoS.

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
