#requires -RunAsAdministrator
param([Parameter(Mandatory=$true)][string]$PublicIp)
$ErrorActionPreference='Stop'
$taskIp=$null
if (-not [Net.IPAddress]::TryParse($PublicIp,[ref]$taskIp) -or $taskIp.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) { throw 'Invalid IPv4.' }
# Authenticate the remote endpoint BEFORE changing only this hostname's mapping.
$taskHealth=& curl.exe --fail --silent --show-error --connect-timeout 5 --max-time 15 --resolve "junglemaze.duckdns.org:443:$PublicIp" https://junglemaze.duckdns.org/health
if ($LASTEXITCODE -ne 0) { throw 'TLS/health failed; hosts unchanged.' }
if (($taskHealth | ConvertFrom-Json).database -ne 'connected') { throw 'Database not ready; hosts unchanged.' }
$taskHosts=Join-Path $env:WINDIR 'System32/drivers/etc/hosts'
$taskOriginal=[IO.File]::ReadAllText($taskHosts)
$taskPattern='(?m)^\s*(?:\d{1,3}\.){3}\d{1,3}\s+junglemaze\.duckdns\.org\s*(?:#.*)?$'
$taskLine="$PublicIp junglemaze.duckdns.org # JungleMaze temporary DNS override"
$taskUpdated=if ($taskOriginal -match $taskPattern) { [regex]::Replace($taskOriginal,$taskPattern,$taskLine) } else { $taskOriginal.TrimEnd()+"`r`n"+$taskLine+"`r`n" }
$taskBackup=Join-Path $env:LOCALAPPDATA 'JungleMaze'
New-Item -ItemType Directory -Force -Path $taskBackup | Out-Null
Copy-Item -LiteralPath $taskHosts -Destination (Join-Path $taskBackup ('hosts.'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'.backup'))
[IO.File]::WriteAllText($taskHosts,$taskUpdated,[Text.UTF8Encoding]::new($false))
Clear-DnsClientCache
Write-Output 'Updated only Jungle Maze hosts mapping after verified TLS; backup retained privately.'
