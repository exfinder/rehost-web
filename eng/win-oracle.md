# win-oracle experiments

Findings behind `win-oracle.sh`'s defaults. Script usage: run it with no args.
Instance shape, timings, and prices are a 2026-08-09 snapshot; verify current
AWS state before provisioning or cost decisions.

## Current shape

Spot `t3a.xlarge` (persistent, stop-on-interruption), 40GB gp3, `unlimited`
credits, baked AMI via SSM `/win-oracle/ami`, repo at `C:\repos\rehost-webforms`.
Launch to usable login ~1m40s. Idle-stop after 60 min; `ssh win-oracle` wakes it.

## Measurements

| | cold build | warm | historical suite | peak RAM |
|---|---|---|---|---|
| t3a.medium 2/4 | 481s | 34s | ~200s | 3353 / 4028 |
| t3a.xlarge 4/16 | 305s | 21s | 164s | 4479 / 16220 |
| t3a.2xlarge 8/32 | — | 28s | 145s | 3309 / 32476 |

- 4GB is too small: peak exceeds a medium's *total* RAM.
- Warm builds are not CPU-bound (~21-28s on 4 and 8 cores). Only cold builds scale.
- 2x vCPU buys ~1.6x cold build. Sub-linear.

## Cost

Windows bills **per rounded-up hour**; every wake costs a full hour, so batching
work beats scattered check-ins. IPv4 $0.005/hr while running, $0 stopped.
Windows license is per-vCPU, which makes burstable far cheaper than
fixed-performance: every 4vCPU/8GB shape costs more than t3a.xlarge (4/16).

At capture time, Spot t3a.xlarge was $0.211/hr versus $0.2464 on-demand;
stopped storage was about $4.80/month
(volume + snapshot).

`standard` credit mode was tried and abandoned — credits do not survive a stop,
so every wake throttles to baseline within minutes.

## Traps

- OpenSSH's firewall rule installs scoped to **Private**; an EC2 NIC is
  **Public**. Port 22 silently drops despite sshd running.
- PowerShell line continuation is a backtick. A `\` in EC2 user-data fails
  invisibly — only visible via `ssm send-command` reading `EC2Launch*\err.tmp`.
- Port 22 answering ≠ usable: user-data writes `authorized_keys` last.
- sshd caches PATH at service start; anything installed later needs a restart
  before `git-receive-pack` resolves. An open ControlMaster hides that restart.
- `ssh` runs ProxyCommand via `/bin/sh -c`; no exec bit gives only
  "Connection closed by UNKNOWN port 65535".
- Locating `Microsoft.WebApplication.targets` by `-Recurse` costs minutes once
  WebBuildTools is installed.
- A **persistent Spot request outlives its instance** and provisions a
  replacement, so `destroy` must cancel it first.
- Spot forces `InstanceInitiatedShutdownBehavior=terminate`: `Stop-Computer`
  would delete the box. The watchdog stops via the EC2 API instead.
- After a stop, a Spot request rejects `StartInstances` with
  `IncorrectSpotRequestState` for 1-2 min.
- A volume restored from snapshot lazy-loads from S3: first build on a fresh
  instance is ~2x slower (53s vs 28s).

## Test suite

`dotnet test Rehost.WebForms.slnx` (whole solution) is supported: after the
2026-08 suite refactoring it passed three consecutive clean runs on this 4-vCPU
host with no parallelism capping. The historical `*OverKestrel*`
cross-project flake predated process consolidation. The abort-scenario flake was
a trace-journal file-sharing race in the harness and is resolved.

The parity hosts are ordinary members of the main solution; building it (in
any configuration) is all the parity gates need.

## Framework binary readings

`ilspycmd` is installed globally on this host. Methodology and the
published-source-lags-binaries finding:
[windows-validation-host.md](../docs/windows-validation-host.md).
## AMI

Not sysprepped: SSH host key survives (no `known_hosts` churn), but EC2Launch's
`.run-once` marker is baked, so **user-data never re-runs** and the baked public
key is the only key that will work. Rebake to change it.

Rebake = `create-image`, then repoint `/win-oracle/ami`. Nothing in the script
creates images; `launch` only reads the parameter and falls back to the stock
Microsoft image when it is absent.
