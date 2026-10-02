# Windows host CPU throttling over SSH

If SSH-launched validation is CPU-throttled, check Windows execution-speed QoS.
Frequency throttling and hybrid-core placement are separate host concerns; changing
process priority is not a substitute for configuring QoS.

## The frequency clamp: a machine-wide registry setting

`PowerThrottlingOff=1` under
`HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling` disables the
frequency clamp machine-wide. It needs an elevated merge and a reboot.

This is machine-wide host state, not repository configuration. Verify it before
changing another host; enabling it requires administrator approval and a reboot.

## The E-core confinement: pinning each process at High QoS

The wrapper sets High QoS for each process and polls descendants because the
policy is not inherited. `eng/Invoke-Unthrottled.ps1` owns this behavior.

Windows names the levels High, Medium, Low, Eco, Multimedia and Deadline. The
policy being disabled is execution speed throttling, hence the API constant
`PROCESS_POWER_THROTTLING_EXECUTION_SPEED`:

| ControlMask | StateMask | result |
| --- | --- | --- |
| `EXECUTION_SPEED` | `EXECUTION_SPEED` | forced EcoQoS |
| `EXECUTION_SPEED` | `0` | pinned at High QoS — what the wrapper sets |
| `0` | `0` | system-managed, the throttled default over SSH |

```powershell
pwsh -NoProfile -File eng\Invoke-Unthrottled.ps1 -Command 'dotnet test Rehost.Web.slnx --no-build'
```

A copy lives at `~\bin\Invoke-Unthrottled.ps1` so the wrapper
survives a checkout reset. It exits with the wrapped command's exit code.

Invoke it as a child process — `pwsh -NoProfile -File ...`, as above — whenever
the output has to be captured or filtered. The wrapped command inherits the
console instead of being redirected, so calling the script in-process
(`& Invoke-Unthrottled.ps1 ...`) prints normally but assigns nothing:
`$out = & Invoke-Unthrottled.ps1 ...; $out | Select-String 'failed:'` finds
nothing and reads as a clean run. Reading the ssh call's own output is always
safe, since over ssh the inherited console is the connection.

From a one-shot `ssh` call the trailing `; exit $LASTEXITCODE` is required, for
the reason given in
[windows-validation-host.md](windows-validation-host.md#exit-codes-over-ssh):

```bash
ssh winbox 'pwsh -NoProfile -File $HOME\bin\Invoke-Unthrottled.ps1 -WorkingDirectory $HOME\source\repos\rehost-web -Command "dotnet test Rehost.Web.slnx --no-build"; exit $LASTEXITCODE'
```

## Interpreting slow rounds

Repeat a cold round before treating timing as a regression. Fixed-timeout tests
remain sensitive to contention; host tuning does not correct fragile assertions.
