param(
    [Parameter(Mandatory=$true)][string]$Player,
    [Parameter(Mandatory=$true)][string]$Scenario,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$ApiBaseUrl='https://junglemaze.duckdns.org',
    [ValidateRange(1,10)][int]$Clients=6,
    [switch]$ReconnectOnly,
    [switch]$ForceAutoFallback,
    # Only an in-process lifecycle controller may request the private restart fixture.
    [switch]$ReturnFixture,
    [object]$Fixture
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$taskUri=[Uri]$ApiBaseUrl
if ($taskUri.Scheme -ne 'https' -or $taskUri.UserInfo -or $taskUri.Query -or $taskUri.Fragment) { throw 'Remote Unity API requires a clean HTTPS origin.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskReport=[ordered]@{schemaVersion=1;status='FAIL';endpoint=$ApiBaseUrl;physicalDeviceCount=1;checks=@();cleanupErrors=@()}
# Windows PowerShell5 shares ServicePoint DNS across HttpClient instances.
# Re-resolve on a new connection after EC2 Stop/Start, without IP pinning or TLS bypass.
$taskDnsRefresh=[Net.ServicePointManager]::DnsRefreshTimeout
[Net.ServicePointManager]::DnsRefreshTimeout=0
$taskHandler=[System.Net.Http.HttpClientHandler]::new()
$taskHandler.AllowAutoRedirect=$false
$taskHttp=[System.Net.Http.HttpClient]::new($taskHandler)
$taskHttp.Timeout=[TimeSpan]::FromSeconds(12)
$taskRoom=$null; $taskToken=$null; $taskGuest=$null
$taskPreviousCode=$env:NET_ROOM_CODE
function Api([string]$Method,[string]$Path,[object]$Body=$null,[string]$Token='',[string]$Guest='') {
    $request=[System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method),$ApiBaseUrl.TrimEnd('/')+$Path)
    if ($null -ne $Body) { $request.Content=[System.Net.Http.StringContent]::new(($Body | ConvertTo-Json -Compress),[Text.Encoding]::UTF8,'application/json') }
    if ($Token) { $request.Headers.Authorization=[System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$Token) }
    if ($Guest) { $request.Headers.Add('X-Guest-Credential',$Guest) }
    try {
        $response=$taskHttp.SendAsync($request).GetAwaiter().GetResult()
        try { $content=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); return @{status=[int]$response.StatusCode;json=$(if ($content) { try { $content | ConvertFrom-Json } catch { $null } } else { $null })} }
        finally { $response.Dispose() }
    } finally { $request.Dispose() }
}
function Check([string]$Name,[bool]$Passed) {
    $taskReport.checks += @{name=$Name;status=$(if ($Passed) {'PASS'} else {'FAIL'})}
    if (-not $Passed) { throw "Check failed: $Name" }
}
try {
    # No pin/TLS bypass: a real Unity player must be able to resolve and verify this origin itself.
    $health=Api GET '/health'; Check 'HTTPS certificate and API health' ($health.status -eq 200)
    $missing=Api POST '/api/rooms' @{name='Denied';maxPlayers=10}; Check 'Room creation without JWT denied' ($missing.status -eq 401)
    $invalid=Api POST '/api/rooms' @{name='Denied';maxPlayers=10} 'invalid.jwt.signature'; Check 'Invalid JWT denied' ($invalid.status -eq 401)
    $guest=Api POST '/api/guests' @{}; Check 'Guest profile over HTTPS' ($guest.status -eq 201 -or $guest.status -eq 200)
    $taskGuest=$guest.json.credential
    if (-not $taskGuest) { $taskGuest=$guest.json.proof }
    Check 'Guest credential present' (-not [string]::IsNullOrEmpty($taskGuest))
    $denied=Api POST '/api/rooms' @{name='Denied';maxPlayers=10} '' $taskGuest; Check 'Guest cannot create rooms' ($denied.status -eq 401)
    if (-not $Fixture) {
        $bytes=New-Object byte[] 24; $rng=[Security.Cryptography.RandomNumberGenerator]::Create();$rng.GetBytes($bytes);$rng.Dispose()
        $Fixture=[pscustomobject]@{username=('onl'+[DateTime]::UtcNow.ToString('MMddHHmmss')+[Guid]::NewGuid().ToString('N').Substring(0,6));password=[Convert]::ToBase64String($bytes)}
        $registered=Api POST '/api/auth/register' @{username=$Fixture.username;password=$Fixture.password}
        Check 'Register test account' ($registered.status -eq 201)
    } else {
        $duplicate=Api POST '/api/auth/register' @{username=$Fixture.username;password=$Fixture.password}
        Check 'Same account persisted after restart' ($duplicate.status -eq 409)
    }
    $login=Api POST '/api/auth/login' @{username=$Fixture.username;password=$Fixture.password}
    Check 'Login after verified registration' ($login.status -eq 200 -and $login.json.accessToken)
    $taskToken=$login.json.accessToken
    $created=Api POST '/api/rooms' @{name='Unity online acceptance';maxPlayers=10} $taskToken
    Check 'JWT creates room' ($created.status -eq 201)
    $taskRoom=$created.json; Check 'Six character join code' ($taskRoom.roomCode -match '^[A-Z0-9]{6}$')
    $config=[ordered]@{mode='Online';apiBaseUrl=$ApiBaseUrl;gameServerHost=$taskUri.Host;gameServerPort=7777;authMode='RoomCode';protocolVersion=3;connectTimeoutMs=5000;transport='Auto';webSocketUrl=('wss://'+$taskUri.Authority+'/game')}
    if ($ForceAutoFallback) { $config.gameServerPort=7779;$taskReport.fallbackTest='Unused UDP7779, same real WSS443 endpoint; no firewall changes' }
    $configPath=Join-Path $OutputDirectory 'client.json'
    $config | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding utf8
    # Force WSS on half the real players; Auto on the others proves route selection
    # without relying on the current network to happen to block UDP.
    $wssPath=Join-Path $OutputDirectory 'client.wss.json'
    $wssConfig=[ordered]@{};foreach ($key in $config.Keys) { $wssConfig[$key]=$config[$key] };$wssConfig.transport='Wss'
    $wssConfig | ConvertTo-Json | Set-Content -LiteralPath $wssPath -Encoding utf8
    $env:NET_ROOM_CODE=$taskRoom.roomCode
    $taskScenario=Get-Content -LiteralPath $Scenario -Raw | ConvertFrom-Json
    if ($ReconnectOnly) {
        $Scenario=Join-Path $OutputDirectory 'reconnect-scenario.json'
        $taskScenario=[pscustomobject]@{schemaVersion=1;id='restart-rejoin';maxDurationSeconds=35;minConnectedSeconds=5;expectedPlayers=1;requireMovement=$false;requireZone=$false;requireHpDamage=$false;requireResult=$false;requireReplay=$false;requireHistory=$false;minInputHz=0;driver='movement';extraAssertions=@()}
        $taskScenario | ConvertTo-Json | Set-Content -LiteralPath $Scenario -Encoding utf8
        $Clients=1
    }
    if ($env:AWS_ROOM_DEADLINE_UTC) {
        $left=([DateTime]::Parse($env:AWS_ROOM_DEADLINE_UTC).ToUniversalTime()-[DateTime]::UtcNow).TotalSeconds
        if ($left -lt ([double]$taskScenario.maxDurationSeconds+45)) { throw 'Not enough AWS soft budget for this full scenario; Stop without shortening acceptance.' }
    }
    $routes=@();for ($i=0;$i -lt $Clients;$i++) { $routes+= $(if (-not $ReconnectOnly -and -not $ForceAutoFallback -and $i -lt [Math]::Floor($Clients/2)) {$wssPath} else {$configPath}) }
    & (Join-Path $PSScriptRoot 'test-unity-online.ps1') -Player $Player -Config $configPath -ClientConfigs $routes -Scenario $Scenario -Clients $Clients -OutputDirectory (Join-Path $OutputDirectory 'players')
    $summaryPath=Join-Path $OutputDirectory 'players/report.json'
    if (-not (Test-Path -LiteralPath $summaryPath)) { throw 'Unity launcher did not produce report.json' }
    $summary=Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    Check 'Actual Windows players completed scenario' ($summary.status -eq 'PASS')
    if ($ForceAutoFallback) { Check 'Auto selected WSS after deliberate UDP failure' (@($summary.clients | Where-Object { $_.selectedTransport -ne 'Wss' -or -not $_.fallbackReason }).Count -eq 0) }
    elseif (-not $ReconnectOnly) { Check 'Forced WSS players actually joined through WSS' (@($summary.clients | Where-Object selectedTransport -eq 'Wss').Count -ge [Math]::Floor($Clients/2)) }
    $taskReport.status='PASS'
} catch {
    # Never persist HTTP bodies, tokens, roomCode or exceptions containing request objects.
    $taskReport.reason='Remote test failed; see sanitized check list and player evidence. DNS/TLS/connectivity failures are BLOCKED.'
    $taskReport.errorType=$_.Exception.GetType().FullName
    if ($taskReport.checks.Count -eq 0) { $taskReport.status='BLOCKED' }
} finally {
    $env:NET_ROOM_CODE=$taskPreviousCode
    if ($taskRoom -and $taskToken) {
        for ($attempt=0;$attempt -lt 4;$attempt++) {
            try { $deleted=Api DELETE ('/api/rooms/'+$taskRoom.id) $null $taskToken; if ($deleted.status -eq 204 -or $deleted.status -eq 404) { $taskRoom=$null;break } } catch { }
            Start-Sleep -Seconds 1
        }
        if ($taskRoom) { $taskReport.cleanupErrors += 'Room removal not confirmed'; $taskReport.status='FAIL' }
    }
    $taskHttp.Dispose();$taskHandler.Dispose()
    [Net.ServicePointManager]::DnsRefreshTimeout=$taskDnsRefresh
    $taskReport | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'report.json') -Encoding utf8
}
if ($taskReport.status -ne 'PASS') { throw ('Remote Unity acceptance '+$taskReport.status) }
# Credential stays in caller memory only for a guarded Stop/Start check, never report/argv.
if ($ReturnFixture) { return $Fixture }
