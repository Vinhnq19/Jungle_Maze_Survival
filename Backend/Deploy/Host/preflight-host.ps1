param([ValidateSet('public','vpn')][string]$Mode='public',[Parameter(Mandatory=$true)][string]$EnvFile,[switch]$StaticOnly,[switch]$OutputJson)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$backend=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$report=[ordered]@{mode=$Mode;scope=$(if ($StaticOnly) {'static'} else {'host-readiness'});allPassed=$true;checks=@()}
function Check([string]$Name,[bool]$Ok,[string]$Detail) {
    $report.checks+=@{name=$Name;status=$(if ($Ok) {'PASS'} else {'FAIL'});message=$Detail}
    if (-not $Ok) { $report.allPassed=$false }
}
$values=@{}
try {
    foreach ($line in Get-Content -LiteralPath $EnvFile -Encoding utf8) {
        $v=$line.Trim();if (-not $v -or $v.StartsWith('#')) { continue }
        if ($v -notmatch '^([A-Z][A-Z0-9_]*)=(.*)$') { throw 'Malformed environment entry' }
        if ($values.ContainsKey($Matches[1])) { throw 'Duplicate environment key' }
        $values[$Matches[1]]=$Matches[2].Trim().Trim([char[]]@([char]39,[char]34))
    }
    $secrets=@($values['JWT_SIGNING_KEY'],$values['ROOM_CONTROL_TOKEN'],$values['MATCH_RESULT_TOKEN'])
    Check 'PrivateSecrets' (@($secrets | Where-Object { -not $_ -or $_.Length -lt 32 -or $_ -match 'REPLACE|EXAMPLE' }).Count -eq 0 -and @($secrets | Select-Object -Unique).Count -eq 3) 'Three distinct secrets, minimum32 characters, no placeholders; values never printed'
    Check 'SwaggerProtection' ($values['SWAGGER_USER'] -and $values['SWAGGER_HASH'] -match '^\$2[aby]\$\d\d\$.{53}$') 'Configured Swagger user and bcrypt hash'
    Check 'JoinCode' (-not [string]::IsNullOrWhiteSpace($values['ROOM_CODE'])) 'Default RoomCode configured'
    if ($Mode -eq 'public') {
        $domain=$values['HOST_DOMAIN']
        Check 'PublicDomain' ($domain -and $domain -match '^[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$' -and $domain -ne 'junglemaze.duckdns.org' -and $domain -notmatch 'example\.(com|org)$') 'Real selfhost domain distinct from AWS'
        Check 'PublicMain' ($values['MATCH_PROFILE'] -eq 'main' -and $values['PUBLIC_HOSTING'] -eq 'true' -and $values['ALLOW_LOCAL_HTTP'] -ne 'true') 'Public main profile and HTTPS-only API'
        Check 'AcmeEmail' ($values['ACME_EMAIL'] -match '^[^@\s]+@[^@\s]+\.[^@\s]+$') 'ACME email configured'
    } else {
        Check 'VpnBoundary' ($values['PUBLIC_HOSTING'] -eq 'false' -and $values['ALLOW_LOCAL_HTTP'] -eq 'true') 'Private Docker API behind HTTPS Tailscale Serve'
    }
    $render=& docker compose -f (Join-Path $backend 'compose.host.yaml') --env-file $EnvFile --profile $Mode config --quiet 2>&1
    Check 'ActualComposeConfiguration' ($LASTEXITCODE -eq 0) 'Rendered actual supplied env, no mocked replacement values'
    if (-not $StaticOnly) {
        $null=& docker info --format '{{.ServerVersion}}' 2>&1;Check 'DockerEngine' ($LASTEXITCODE -eq 0) 'Docker daemon reachable'
        if ($Mode -eq 'vpn') {
            $ts='C:/Program Files/Tailscale/tailscale.exe'
            Check 'TailscaleInstalled' (Test-Path -LiteralPath $ts) 'Tailscale required for VPN route'
            if (Test-Path -LiteralPath $ts) { $state=& $ts status --json 2>$null | ConvertFrom-Json;Check 'TailscaleRunning' ($state.BackendState -eq 'Running') 'VPN authenticated; remote peer test still required' }
        }
    }
} catch { Check 'PreflightExecution' $false ('Preflight failed: '+$_.Exception.GetType().Name) }
if ($OutputJson) { $report | ConvertTo-Json -Depth 6 } else { $report.checks | Select-Object name,status,message | Format-Table -AutoSize }
if (-not $report.allPassed) { exit 1 }
