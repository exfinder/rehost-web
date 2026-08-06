$ErrorActionPreference = 'Stop'

$features = Get-WindowsFeature Web-Server, Web-Asp-Net45
if ($features | Where-Object InstallState -ne 'Installed') {
    Install-WindowsFeature Web-Server, Web-Asp-Net45 | Out-Null
}

Import-Module IISAdministration
$site = Get-IISSite -Name 'WireRig' -ErrorAction SilentlyContinue
if (-not $site) {
    New-IISSite -Name 'WireRig' -PhysicalPath 'C:\readings\wire-rig\app' -BindingInformation '*:8099:'
}
Start-IISSite -Name 'WireRig' -ErrorAction SilentlyContinue

$pool = (Get-IISServerManager).ApplicationPools['DefaultAppPool']
"pool CLR=$($pool.ManagedRuntimeVersion) pipeline=$($pool.ManagedPipelineMode)"
"site state=$((Get-IISSite -Name 'WireRig').State)"
