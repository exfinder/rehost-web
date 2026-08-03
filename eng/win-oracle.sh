#!/usr/bin/env bash
# Windows Server Core oracle host on EC2: launch, start/stop, direct SSH.
# Port 22 is open only to the launching machine's public IP; key auth only.
set -euo pipefail

REGION="${WIN_ORACLE_REGION:-$(aws configure get region 2>/dev/null || echo us-east-1)}"
NAME="${WIN_ORACLE_NAME:-rehost-win-oracle}"
INSTANCE_TYPE="${WIN_ORACLE_TYPE:-t3a.medium}"
VOLUME_GB="${WIN_ORACLE_VOLUME_GB:-40}"
SSH_KEY="${WIN_ORACLE_SSH_KEY:-$HOME/.ssh/id_ed25519}"
SSH_ALIAS="${WIN_ORACLE_SSH_ALIAS:-win-oracle}"
SSH_CONFIG_FRAGMENT="${WIN_ORACLE_SSH_CONFIG:-$HOME/.ssh/config.d/$SSH_ALIAS}"
KNOWN_HOSTS="${WIN_ORACLE_KNOWN_HOSTS:-$HOME/.ssh/known_hosts.win-oracle}"
AMI_PARAM="${WIN_ORACLE_AMI_PARAM:-/aws/service/ami-windows-latest/Windows_Server-2025-English-Core-Base}"
DOTNET_CHANNEL="${WIN_ORACLE_DOTNET_CHANNEL:-10.0}"
IDLE_MINUTES="${WIN_ORACLE_IDLE_MINUTES:-60}"
# standard throttles to the 20-30%% baseline once credits run out, which a build
# exhausts within minutes of a wake; unlimited bills the surplus instead.
CREDIT_MODE="${WIN_ORACLE_CREDIT_MODE:-unlimited}"
STOP_CRON="${WIN_ORACLE_STOP_CRON:-cron(0 22 * * ? *)}"
CACHE_TTL="${WIN_ORACLE_CACHE_TTL:-3600}"
ENDPOINT_CACHE="${WIN_ORACLE_ENDPOINT_CACHE:-$HOME/.cache/win-oracle/$NAME}"

ROLE_NAME="$NAME-ssm"
PROFILE_NAME="$NAME-ssm"
SG_NAME="$NAME-ssh"
SCHED_ROLE_NAME="$NAME-scheduler"

SCRIPT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")"

aws_() { aws --region "$REGION" "$@"; }

die() { echo "error: $*" >&2; exit 1; }

require_tools() {
  command -v aws >/dev/null || die "aws CLI not found"
  aws_ sts get-caller-identity >/dev/null 2>&1 || die "aws credentials not configured (run: aws configure)"
}

my_ip() {
  local ip
  ip=$(curl -fsS --max-time 10 https://checkip.amazonaws.com 2>/dev/null | tr -d '[:space:]') \
    || die "could not determine your public IP"
  [[ "$ip" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "unexpected public IP response: $ip"
  echo "$ip"
}

resolve_ami() {
  local ami
  if ! ami=$(aws_ ssm get-parameter --name "$AMI_PARAM" --query Parameter.Value --output text 2>/dev/null); then
    echo "AMI parameter not found: $AMI_PARAM" >&2
    echo "available Core images:" >&2
    aws_ ssm get-parameters-by-path \
      --path /aws/service/ami-windows-latest --query 'Parameters[].Name' --output text 2>/dev/null \
      | tr '\t' '\n' | grep -i 'Core-Base' >&2 || true
    exit 1
  fi
  echo "$ami"
}

find_instance() {
  aws_ ec2 describe-instances \
    --filters "Name=tag:Name,Values=$NAME" "Name=instance-state-name,Values=pending,running,stopping,stopped" \
    --query 'Reservations[].Instances[0].InstanceId' --output text 2>/dev/null | grep -v '^None$' || true
}

require_instance() {
  local id
  id=$(find_instance)
  [ -n "$id" ] || die "no instance tagged Name=$NAME in $REGION (run: $0 launch)"
  echo "$id"
}

public_ip() {
  local ip
  ip=$(aws_ ec2 describe-instances --instance-ids "$1" \
    --query 'Reservations[].Instances[].PublicIpAddress' --output text)
  [ -n "$ip" ] && [ "$ip" != "None" ] || die "instance $1 has no public IP (is it running?)"
  echo "$ip"
}

write_ssh_alias() {
  mkdir -p "$(dirname "$SSH_CONFIG_FRAGMENT")"
  # ssh runs ProxyCommand through /bin/sh -c; without the exec bit it fails
  # instantly and ssh reports only "Connection closed by UNKNOWN port 65535".
  [ -x "$SCRIPT_PATH" ] || chmod +x "$SCRIPT_PATH"
  # HostName is the alias itself, never an address: ProxyCommand resolves the
  # current IP per connection, so a stop/start cannot leave this stale.
  cat >"$SSH_CONFIG_FRAGMENT" <<CONFIG
Host $SSH_ALIAS
  HostName $SSH_ALIAS
  User Administrator
  IdentityFile $SSH_KEY
  IdentitiesOnly yes
  UserKnownHostsFile $KNOWN_HOSTS
  StrictHostKeyChecking accept-new
  ProxyCommand $SCRIPT_PATH connect
  ControlMaster auto
  ControlPath ~/.ssh/cm-%r@%h:%p
  ControlPersist 10m
CONFIG
  chmod 600 "$SSH_CONFIG_FRAGMENT"
  echo "ssh alias '$SSH_ALIAS' -> $SCRIPT_PATH connect ($SSH_CONFIG_FRAGMENT)"

  if ! grep -qE "^\s*Include\s+.*config\.d" "$HOME/.ssh/config" 2>/dev/null; then
    echo
    echo "  add this as the FIRST line of ~/.ssh/config to activate the alias:"
    echo "    Include config.d/*"
    echo
  fi
}

ensure_instance_profile() {
  if ! aws iam get-instance-profile --instance-profile-name "$PROFILE_NAME" >/dev/null 2>&1; then
    aws iam create-role --role-name "$ROLE_NAME" \
      --assume-role-policy-document '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"ec2.amazonaws.com"},"Action":"sts:AssumeRole"}]}' >/dev/null
    aws iam attach-role-policy --role-name "$ROLE_NAME" \
      --policy-arn arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore >/dev/null
    aws iam create-instance-profile --instance-profile-name "$PROFILE_NAME" >/dev/null
    aws iam add-role-to-instance-profile --instance-profile-name "$PROFILE_NAME" --role-name "$ROLE_NAME" >/dev/null
  fi
  echo "$PROFILE_NAME"
}

find_security_group() {
  local vpc
  vpc=$(aws_ ec2 describe-vpcs --filters Name=isDefault,Values=true --query 'Vpcs[0].VpcId' --output text)
  [ "$vpc" != "None" ] || die "no default VPC in $REGION; set one up or adapt this script"
  aws_ ec2 describe-security-groups \
    --filters "Name=group-name,Values=$SG_NAME" "Name=vpc-id,Values=$vpc" \
    --query 'SecurityGroups[0].GroupId' --output text 2>/dev/null || echo None
}

ensure_security_group() {
  local vpc sg
  sg=$(find_security_group)
  if [ "$sg" = "None" ]; then
    vpc=$(aws_ ec2 describe-vpcs --filters Name=isDefault,Values=true --query 'Vpcs[0].VpcId' --output text)
    sg=$(aws_ ec2 create-security-group --group-name "$SG_NAME" --vpc-id "$vpc" \
      --description "SSH from known addresses only" --query GroupId --output text)
  fi
  echo "$sg"
}

authorize_my_ip() {
  local sg="$1" ip cidr existing
  ip=$(my_ip)
  cidr="$ip/32"
  existing=$(aws_ ec2 describe-security-groups --group-ids "$sg" \
    --query "SecurityGroups[0].IpPermissions[?FromPort==\`22\`].IpRanges[].CidrIp" --output text)
  if grep -qw -- "$cidr" <<<"$existing"; then
    echo "$cidr already allowed on 22"
  else
    aws_ ec2 authorize-security-group-ingress --group-id "$sg" \
      --ip-permissions "IpProtocol=tcp,FromPort=22,ToPort=22,IpRanges=[{CidrIp=$cidr,Description=$NAME}]" >/dev/null
    echo "allowed $cidr on 22"
  fi
}

user_data() {
  local pubkey="$1"
  cat <<POWERSHELL
<powershell>
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
Set-Service -Name sshd -StartupType Automatic
Start-Service sshd

# The capability's firewall rule is scoped to the Private profile, but an EC2
# NIC classifies as Public, so inbound 22 is dropped until this is widened.
Set-NetFirewallRule -Name OpenSSH-Server-In-TCP -Profile Any

\$ProgressPreference = "SilentlyContinue"
try {
  Invoke-RestMethod https://aka.ms/install-powershell.ps1 -OutFile "\$env:TEMP\\install-powershell.ps1"
  & "\$env:TEMP\\install-powershell.ps1" -UseMSI -Quiet
} catch {
  Write-Output "pwsh install failed: \$_"
}

# Pointing DefaultShell at a missing binary would break every SSH session, so
# only switch once the install is confirmed on disk.
\$pwsh = "C:\\Program Files\\PowerShell\\7\\pwsh.exe"
\$shell = if (Test-Path \$pwsh) { \$pwsh } else { "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe" }
New-ItemProperty -Path "HKLM:\\SOFTWARE\\OpenSSH" -Name DefaultShell -Value \$shell -PropertyType String -Force | Out-Null

\$keyFile = "C:\\ProgramData\\ssh\\administrators_authorized_keys"
Set-Content -Path \$keyFile -Value '$pubkey' -Encoding ascii
icacls \$keyFile /inheritance:r /grant "Administrators:F" /grant "SYSTEM:F"

\$cfg = "C:\\ProgramData\\ssh\\sshd_config"
(Get-Content \$cfg) -replace '^#?PasswordAuthentication.*','PasswordAuthentication no' | Set-Content \$cfg
Restart-Service sshd
</powershell>
POWERSHELL
}

# Gates on an authenticated command, not on port 22 answering: sshd starts
# early in user-data but administrators_authorized_keys is written last, so the
# port accepts connections for a minute or so before any login can succeed.
# DefaultShell is set before the key file, so a successful auth also means pwsh
# is already wired up.
wait_for_ssh() {
  local waited=0
  echo -n "waiting for sshd (user-data installs it on first boot)"
  until ssh -i "$SSH_KEY" -o IdentitiesOnly=yes -o BatchMode=yes \
      -o ConnectTimeout=10 -o StrictHostKeyChecking=accept-new \
      -o UserKnownHostsFile="$KNOWN_HOSTS" -o "ProxyCommand=$SCRIPT_PATH connect" \
      "Administrator@$SSH_ALIAS" exit >/dev/null 2>&1; do
    [ "$waited" -lt 900 ] || { echo; die "no usable ssh login within 15 minutes; check user-data via ssm send-command"; }
    echo -n "."
    sleep 15
    waited=$((waited + 15))
  done
  echo " ready"
}

cmd_launch() {
  require_tools
  [ -f "$SSH_KEY.pub" ] || die "no public key at $SSH_KEY.pub (generate one, or set WIN_ORACLE_SSH_KEY)"
  local existing
  existing=$(find_instance)
  [ -z "$existing" ] || die "instance $existing already tagged Name=$NAME; use '$0 start' or pick another WIN_ORACLE_NAME"

  local ami profile sg pubkey udata
  ami=$(resolve_ami)
  profile=$(ensure_instance_profile)
  sg=$(ensure_security_group)
  authorize_my_ip "$sg"
  pubkey=$(<"$SSH_KEY.pub")
  udata=$(user_data "$pubkey")

  echo "launching $INSTANCE_TYPE from $ami (${VOLUME_GB}GB gp3) in $REGION"

  local id attempt=0
  # The instance profile is not immediately visible to EC2 after creation.
  until id=$(aws_ ec2 run-instances \
      --image-id "$ami" \
      --instance-type "$INSTANCE_TYPE" \
      --security-group-ids "$sg" \
      --associate-public-ip-address \
      --iam-instance-profile "Name=$profile" \
      --credit-specification "CpuCredits=$CREDIT_MODE" \
      --metadata-options "HttpTokens=required" \
      --block-device-mappings "[{\"DeviceName\":\"/dev/sda1\",\"Ebs\":{\"VolumeSize\":$VOLUME_GB,\"VolumeType\":\"gp3\",\"DeleteOnTermination\":true}}]" \
      --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=$NAME}]" \
      --user-data "$udata" \
      --query 'Instances[0].InstanceId' --output text 2>/dev/null); do
    attempt=$((attempt + 1))
    [ "$attempt" -lt 10 ] || die "run-instances failed 10 times; rerun to see the raw error"
    sleep 6
  done

  echo "instance $id"
  aws_ ec2 wait instance-running --instance-ids "$id"
  # A new instance always presents a new host key, but known_hosts pins the key
  # to the alias, so a relaunch under the same name reads as a changed key and
  # ssh refuses with a MITM warning that points nowhere near the real cause.
  ssh-keygen -R "$SSH_ALIAS" -f "$KNOWN_HOSTS" >/dev/null 2>&1 || true
  write_ssh_alias
  wait_for_ssh

  echo
  echo "next:"
  echo "  $0 provision   # toolchain + idle-stop watchdog"
  echo "  $0 schedule    # nightly stop safety net"
  echo "  ssh $SSH_ALIAS # wakes it on demand from now on"
}

cmd_status() {
  require_tools
  local id
  id=$(require_instance)
  aws_ ec2 describe-instances --instance-ids "$id" \
    --query 'Reservations[].Instances[].{Id:InstanceId,State:State.Name,Type:InstanceType,PublicIp:PublicIpAddress}' \
    --output table
}

cmd_start() {
  require_tools
  local id ip
  id=$(require_instance)
  aws_ ec2 start-instances --instance-ids "$id" >/dev/null
  aws_ ec2 wait instance-running --instance-ids "$id"
  ip=$(public_ip "$id")
  echo "$id running at $ip"
  authorize_my_ip "$(find_security_group)"
  write_ssh_alias
  wait_for_ssh
}

cmd_stop() {
  require_tools
  local id
  id=$(require_instance)
  aws_ ec2 stop-instances --instance-ids "$id" >/dev/null
  rm -f "$ENDPOINT_CACHE"
  echo "$id stopping; compute billing ends once it is stopped"
}

cmd_allow_ip() {
  require_tools
  authorize_my_ip "$(ensure_security_group)"
}

cmd_revoke_ips() {
  require_tools
  local sg cidrs
  sg=$(find_security_group)
  [ "$sg" != "None" ] || die "no security group $SG_NAME in $REGION"
  cidrs=$(aws_ ec2 describe-security-groups --group-ids "$sg" \
    --query "SecurityGroups[0].IpPermissions[?FromPort==\`22\`].IpRanges[].CidrIp" --output text)
  [ -n "$cidrs" ] || { echo "no port 22 rules to remove"; return; }
  for cidr in $cidrs; do
    aws_ ec2 revoke-security-group-ingress --group-id "$sg" \
      --ip-permissions "IpProtocol=tcp,FromPort=22,ToPort=22,IpRanges=[{CidrIp=$cidr}]" >/dev/null
    echo "revoked $cidr"
  done
}

# ssh ProxyCommand: stdout is the tunnel, so every message goes to stderr.
cmd_connect() {
  local id state ip waited=0 cached age

  # The cached address is only a hint: it is proven by connecting to it, so a
  # stale entry costs one refused connection rather than a broken session.
  # Without that proof no TTL would be safe, since the idle watchdog can stop
  # the instance at any time and each start assigns a new address.
  if [ -f "$ENDPOINT_CACHE" ]; then
    age=$(( $(date +%s) - $(stat -f %m "$ENDPOINT_CACHE" 2>/dev/null || echo 0) ))
    if [ "$age" -lt "$CACHE_TTL" ]; then
      cached=$(<"$ENDPOINT_CACHE")
      if [ -n "$cached" ] && nc -z -G 2 "$cached" 22 2>/dev/null; then
        exec nc "$cached" 22
      fi
    fi
  fi

  read -r id state ip <<<"$(aws_ ec2 describe-instances \
    --filters "Name=tag:Name,Values=$NAME" "Name=instance-state-name,Values=pending,running,stopping,stopped" \
    --query 'Reservations[].Instances[0].[InstanceId,State.Name,PublicIpAddress]' --output text 2>/dev/null)"
  [ -n "${id:-}" ] && [ "$id" != "None" ] || die "no instance tagged Name=$NAME in $REGION"

  if [ "$state" != "running" ]; then
    echo "$SSH_ALIAS: instance is $state, starting it" >&2
    aws_ ec2 start-instances --instance-ids "$id" >/dev/null
    aws_ ec2 wait instance-running --instance-ids "$id"
    authorize_my_ip "$(ensure_security_group)" >&2
    ip=$(public_ip "$id")
    echo "$SSH_ALIAS: running at $ip, waiting for sshd" >&2
  fi
  [ -n "${ip:-}" ] && [ "$ip" != "None" ] || ip=$(public_ip "$id")

  until nc -z -G 5 "$ip" 22 2>/dev/null; do
    [ "$waited" -lt 300 ] || die "$SSH_ALIAS: sshd unreachable at $ip after 5 minutes"
    sleep 5
    waited=$((waited + 5))
  done
  mkdir -p "$(dirname "$ENDPOINT_CACHE")"
  printf '%s' "$ip" >"$ENDPOINT_CACHE"
  exec nc "$ip" 22
}

provision_script() {
  sed -e "s|@DOTNET_CHANNEL@|$DOTNET_CHANNEL|g" -e "s|@IDLE_MINUTES@|$IDLE_MINUTES|g" <<'PS1'
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$dotnetDir = 'C:\Program Files\dotnet'
if (Test-Path "$dotnetDir\sdk") {
  'dotnet sdk: already present'
} else {
  Invoke-RestMethod https://dot.net/v1/dotnet-install.ps1 -OutFile "$env:TEMP\dotnet-install.ps1"
  # dotnet-install reports progress on the information stream, which pwsh
  # serializes to CLIXML over a non-interactive ssh session and floods the
  # output with markup. The version is reported by the summary block below.
  & "$env:TEMP\dotnet-install.ps1" -Channel '@DOTNET_CHANNEL@' -InstallDir $dotnetDir 6>$null
  'dotnet sdk: installed'
}

$machinePath = [Environment]::GetEnvironmentVariable('Path','Machine')
if ($machinePath -notlike "*$dotnetDir*") {
  [Environment]::SetEnvironmentVariable('Path', "$machinePath;$dotnetDir", 'Machine')
}
$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine')

$refAsm = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8.1\System.Web.dll'
# Wildcards cover the year, edition and toolset segments (2022\BuildTools\...\v17.0)
# without walking the whole VS tree, which takes minutes once WebBuildTools is in.
$webTargets = 'C:\Program Files (x86)\Microsoft Visual Studio\*\*\MSBuild\Microsoft\VisualStudio\v*\WebApplications\Microsoft.WebApplication.targets'

if ((Test-Path $refAsm) -and (Test-Path $webTargets)) {
  'build tools: already provisioned'
} else {
  Invoke-RestMethod https://aka.ms/vs/17/release/vs_BuildTools.exe -OutFile "$env:TEMP\vs_BuildTools.exe"
  $vsArgs = @('--quiet','--wait','--norestart','--nocache')
  foreach ($c in @(
    'Microsoft.VisualStudio.Workload.MSBuildTools',
    'Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools',
    'Microsoft.VisualStudio.Workload.WebBuildTools',
    'Microsoft.Net.Component.4.8.1.TargetingPack',
    'Microsoft.Net.Component.4.8.1.SDK')) {
    $vsArgs += '--add'; $vsArgs += $c
  }
  $p = Start-Process -Wait -PassThru -FilePath "$env:TEMP\vs_BuildTools.exe" -ArgumentList $vsArgs
  # 3010 means installed successfully but a reboot is pending.
  if ($p.ExitCode -notin 0,3010) { throw "vs_BuildTools failed with exit code $($p.ExitCode)" }
}

$idleDir = 'C:\ProgramData\win-oracle'
New-Item -ItemType Directory -Force $idleDir | Out-Null
$watchdog = Join-Path $idleDir 'idle-stop.ps1'

# An EBS-backed instance defaults to InstanceInitiatedShutdownBehavior=stop, so
# a guest shutdown lands in the stopped state and ends compute billing without
# needing any AWS credentials on the box.
Set-Content $watchdog @"
`$stamp = '$idleDir\last-active'
`$idleMinutes = @IDLE_MINUTES@
`$boot = (Get-CimInstance Win32_OperatingSystem).LastBootUpTime

`$busy = @(Get-NetTCPConnection -LocalPort 22 -State Established -ErrorAction SilentlyContinue).Count -gt 0
if (-not `$busy) {
  `$busy = @(Get-Process msbuild,dotnet,VBCSCompiler,vs_BuildTools -ErrorAction SilentlyContinue).Count -gt 0
}

if (`$busy -or -not (Test-Path `$stamp) -or ([datetime](Get-Content `$stamp -Raw) -lt `$boot)) {
  Set-Content `$stamp (Get-Date).ToString('o')
  return
}
if (((Get-Date) - [datetime](Get-Content `$stamp -Raw)).TotalMinutes -ge `$idleMinutes) {
  Stop-Computer -Force
}
"@

Set-Content "$idleDir\last-active" (Get-Date).ToString('o')
$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
  -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$watchdog`""
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) `
  -RepetitionInterval (New-TimeSpan -Minutes 5)
Register-ScheduledTask -TaskName 'win-oracle-idle-stop' -Action $action -Trigger $trigger `
  -User 'SYSTEM' -RunLevel Highest -Force | Out-Null

'--- provisioned ---'
"pwsh              : $($PSVersionTable.PSVersion)"
"dotnet sdk        : $(& dotnet --version)"
"NDP v4 Release    : $((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release)"
"4.8.1 ref asm     : $(Test-Path $refAsm)"
"aspnet_compiler   : $(Test-Path 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe')"
"WebApplication    : $(Test-Path $webTargets)"
"idle-stop task    : $((Get-ScheduledTask -TaskName 'win-oracle-idle-stop' -ErrorAction SilentlyContinue).State) (@IDLE_MINUTES@ min)"
PS1
}

cmd_provision() {
  require_tools
  local encoded
  # -EncodedCommand sidesteps quoting entirely across bash -> ssh -> pwsh.
  encoded=$(provision_script | iconv -f UTF-8 -t UTF-16LE | base64 | tr -d '\n')
  cmd_ssh "pwsh -NoProfile -EncodedCommand $encoded"
}

cmd_alias() {
  write_ssh_alias
}

ensure_scheduler_role() {
  local acct
  acct=$(aws sts get-caller-identity --query Account --output text)
  if ! aws iam get-role --role-name "$SCHED_ROLE_NAME" >/dev/null 2>&1; then
    aws iam create-role --role-name "$SCHED_ROLE_NAME" \
      --assume-role-policy-document "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"Service\":\"scheduler.amazonaws.com\"},\"Action\":\"sts:AssumeRole\",\"Condition\":{\"StringEquals\":{\"aws:SourceAccount\":\"$acct\"}}}]}" >/dev/null
  fi
  aws iam put-role-policy --role-name "$SCHED_ROLE_NAME" --policy-name start-stop-tagged \
    --policy-document "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Action\":[\"ec2:StartInstances\",\"ec2:StopInstances\"],\"Resource\":\"arn:aws:ec2:$REGION:$acct:instance/*\",\"Condition\":{\"StringEquals\":{\"ec2:ResourceTag/Name\":\"$NAME\"}}}]}" >/dev/null
  echo "arn:aws:iam::$acct:role/$SCHED_ROLE_NAME"
}

# Only a stop schedule: waking is on demand via ProxyCommand, and a start
# schedule would bill for hours nobody asked for.
cmd_schedule() {
  require_tools
  local id role target
  id=$(require_instance)
  role=$(ensure_scheduler_role)
  target="{\"Arn\":\"arn:aws:scheduler:::aws-sdk:ec2:stopInstances\",\"RoleArn\":\"$role\",\"Input\":\"{\\\"InstanceIds\\\":[\\\"$id\\\"]}\"}"
  if aws_ scheduler get-schedule --name "$NAME-stop" >/dev/null 2>&1; then
    aws_ scheduler update-schedule --name "$NAME-stop" \
      --schedule-expression "$STOP_CRON" --schedule-expression-timezone UTC \
      --flexible-time-window '{"Mode":"OFF"}' --target "$target" >/dev/null
    echo "updated schedule $NAME-stop -> $STOP_CRON UTC on $id"
  else
    aws_ scheduler create-schedule --name "$NAME-stop" \
      --schedule-expression "$STOP_CRON" --schedule-expression-timezone UTC \
      --flexible-time-window '{"Mode":"OFF"}' --target "$target" >/dev/null
    echo "created schedule $NAME-stop -> $STOP_CRON UTC on $id"
  fi
  if aws_ scheduler get-schedule --name "$NAME-start" >/dev/null 2>&1; then
    aws_ scheduler delete-schedule --name "$NAME-start" >/dev/null
    echo "removed $NAME-start (wake is on demand)"
  fi
}

# Self-contained rather than relying on the Include being wired up, but it goes
# through the same ProxyCommand so it wakes a stopped instance too.
cmd_ssh() {
  exec ssh -i "$SSH_KEY" -o IdentitiesOnly=yes \
    -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile="$KNOWN_HOSTS" \
    -o "ProxyCommand=$SCRIPT_PATH connect" \
    "Administrator@$SSH_ALIAS" "$@"
}

cmd_destroy() {
  require_tools
  local id size
  id=$(require_instance)
  # Report the volume actually attached, not the size a future launch would use.
  size=$(aws_ ec2 describe-volumes --filters "Name=attachment.instance-id,Values=$id" \
    --query 'Volumes[0].Size' --output text 2>/dev/null)
  [ -n "$size" ] && [ "$size" != "None" ] || size="?"
  echo "this terminates $id and deletes its ${size}GB volume. everything on it is lost."
  read -r -p "type the instance id to confirm: " confirm
  [ "$confirm" = "$id" ] || die "not confirmed"
  aws_ ec2 terminate-instances --instance-ids "$id" >/dev/null
  rm -f "$ENDPOINT_CACHE"
  echo "$id terminating; IAM role and security group left in place for reuse"
}

case "${1:-}" in
  launch)     shift; cmd_launch "$@" ;;
  status)     shift; cmd_status "$@" ;;
  start)      shift; cmd_start "$@" ;;
  stop)       shift; cmd_stop "$@" ;;
  allow-ip)   shift; cmd_allow_ip "$@" ;;
  revoke-ips) shift; cmd_revoke_ips "$@" ;;
  alias)      shift; cmd_alias "$@" ;;
  provision)  shift; cmd_provision "$@" ;;
  schedule)   shift; cmd_schedule "$@" ;;
  connect)    shift; cmd_connect "$@" ;;
  ssh)        shift; cmd_ssh "$@" ;;
  destroy)    shift; cmd_destroy "$@" ;;
  *)
    cat <<USAGE
usage: $0 {launch|provision|schedule|status|start|stop|allow-ip|revoke-ips|alias|ssh|destroy}

  launch      create the instance (billable)
  provision   toolchain + idle-stop watchdog; idempotent, safe to re-run
  schedule    nightly stop safety net; removes any leftover start schedule
  start       start a stopped instance and re-authorize your current IP
  stop        stop it; volume charges continue, compute charges do not
  allow-ip    add your current public IP to the port 22 rules
  revoke-ips  drop every port 22 rule
  alias       rewrite the '$SSH_ALIAS' ssh config fragment
  ssh         ssh to it; extra args run as a remote command
  destroy     terminate and delete the volume

  connect     ssh ProxyCommand -- wakes a stopped instance, then pipes to :22.
              Not meant to be run by hand.

The box stops itself after $IDLE_MINUTES min with no ssh session and no build
running; 'ssh $SSH_ALIAS' starts it again on demand.

env overrides: WIN_ORACLE_REGION($REGION) WIN_ORACLE_NAME($NAME)
               WIN_ORACLE_TYPE($INSTANCE_TYPE) WIN_ORACLE_VOLUME_GB($VOLUME_GB)
               WIN_ORACLE_SSH_KEY($SSH_KEY) WIN_ORACLE_SSH_ALIAS($SSH_ALIAS)
               WIN_ORACLE_KNOWN_HOSTS($KNOWN_HOSTS)
               WIN_ORACLE_DOTNET_CHANNEL($DOTNET_CHANNEL)
               WIN_ORACLE_IDLE_MINUTES($IDLE_MINUTES)
               WIN_ORACLE_STOP_CRON($STOP_CRON)
               WIN_ORACLE_CREDIT_MODE($CREDIT_MODE)
               WIN_ORACLE_CACHE_TTL($CACHE_TTL) WIN_ORACLE_ENDPOINT_CACHE($ENDPOINT_CACHE)
USAGE
    exit 1 ;;
esac
