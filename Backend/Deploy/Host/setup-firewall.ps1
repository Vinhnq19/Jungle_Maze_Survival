param([ValidateSet('Show','Enable','Disable')][string]$Action='Show',[ValidateSet('Public','Vpn')][string]$Mode='Public',[ValidateRange(1,65535)][int]$UdpPort=7777)
#Requires -Version 5.1
# Run manually as Administrator. This script never opens DB/control/API/internal WS ports.
$ErrorActionPreference='Stop'
$group='JungleMaze-Host'
if ($Action -eq 'Show') { Get-NetFirewallRule -DisplayGroup $group -ErrorAction SilentlyContinue | Select-Object Name,Enabled,Direction,Action;return }
$principal=[Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run manually as Administrator.' }
Get-NetFirewallRule -DisplayGroup $group -ErrorAction SilentlyContinue | Remove-NetFirewallRule
if ($Action -eq 'Disable') { return }
$remote=if ($Mode -eq 'Vpn') {'100.64.0.0/10'} else {'Any'}
New-NetFirewallRule -Name 'JungleMaze-Host-UDP' -DisplayName 'JungleMaze Host Game UDP' -Group $group -Direction Inbound -Action Allow -Protocol UDP -LocalPort $UdpPort -RemoteAddress $remote | Out-Null
if ($Mode -eq 'Public') {
    New-NetFirewallRule -Name 'JungleMaze-Host-HTTPS' -DisplayName 'JungleMaze Host HTTPS' -Group $group -Direction Inbound -Action Allow -Protocol TCP -LocalPort 443 | Out-Null
    New-NetFirewallRule -Name 'JungleMaze-Host-HTTP' -DisplayName 'JungleMaze Host ACME HTTP' -Group $group -Direction Inbound -Action Allow -Protocol TCP -LocalPort 80 | Out-Null
}
# VPN HTTP8080 is loopback only and needs no inbound firewall allowance.
