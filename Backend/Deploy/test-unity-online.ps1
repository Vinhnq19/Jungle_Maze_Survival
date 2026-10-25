<#
.SYNOPSIS
    Reusable multi-client online readiness test launcher for JungleMaze Unity Windows standalone builds.
.DESCRIPTION
    Launches 2..10 Unity Windows clients to execute automated online readiness scenarios.
    Adheres strictly to the specification contract:
      - Unity CLI args: -onlineTest <scenario> -onlineEvidence <output> -clientConfig <config> -name <name>
      - Screen size: 640x360 windowed mode
      - Passes RoomCode strictly via NET_ROOM_CODE environment variable (never argv or logs)
      - Bounded watchdog timer; verifies evidence status rather than relying on process exit code alone
      - In clean-up, stops ONLY owned processes by matching both PID and start time
      - Produces consolidated report.json and summary.csv with physicalTwoPc: NOT_TESTED
      - Never leaks secrets into commandline, logs, or reports
.PARAMETER Player
    Path to the Unity standalone executable (e.g. Lastaway.exe or JungleMaze.exe).
.PARAMETER Config
    Path to the client configuration JSON file (e.g. client.local-auto.json).
.PARAMETER Scenario
    Path to the scenario JSON file (e.g. local2-accelerated.json or main6-smoke.json).
.PARAMETER Clients
    Number of clients to launch (2..10). If 0 or omitted, uses scenario's expectedPlayers.
.PARAMETER OutputDirectory
    Directory where evidence, logs, report.json, and summary.csv will be saved.
.PARAMETER RoomCode
    Optional room code override. If omitted, uses $env:NET_ROOM_CODE.
.PARAMETER TimeoutSeconds
    Watchdog timeout in seconds. If 0 or omitted, uses scenario's maxDurationSeconds + 10s buffer.
.PARAMETER StaggerMs
    Millisecond delay between launching consecutive clients (default 300ms).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Player,

    [Parameter(Mandatory=$true)]
    [string]$Config,

    [Parameter(Mandatory=$true)]
    [string]$Scenario,

    [Parameter(Mandatory=$false)]
    [int]$Clients = 0,

    [Parameter(Mandatory=$true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory=$false)]
    [string]$RoomCode = $env:NET_ROOM_CODE,

    [Parameter(Mandatory=$false)]
    [int]$TimeoutSeconds = 0,

    [Parameter(Mandatory=$false)]
    [int]$StaggerMs = 100,
    [string[]]$ClientConfigs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../Tests/OnlineReadiness/OnlineReadiness.psm1') -Force

# 1. Validate Player Executable
if (-not (Test-Path -LiteralPath $Player)) {
    throw "Player executable not found at '$Player'."
}
$playerExe = (Resolve-Path -LiteralPath $Player).Path

# 2. Validate Client Config
if (-not (Test-Path -LiteralPath $Config)) {
    throw "Client config JSON not found at '$Config'."
}
$configPath = (Resolve-Path -LiteralPath $Config).Path
try {
    $null = Get-Content -LiteralPath $configPath -Raw -Encoding utf8 | ConvertFrom-Json
} catch {
    throw "Client config at '$configPath' is not valid JSON: $_"
}

# 3. Validate Scenario JSON
if (-not (Test-Path -LiteralPath $Scenario)) {
    throw "Scenario JSON not found at '$Scenario'."
}
$scenarioPath = (Resolve-Path -LiteralPath $Scenario).Path
try {
    $scenarioObj = Get-Content -LiteralPath $scenarioPath -Raw -Encoding utf8 | ConvertFrom-Json
} catch {
    throw "Scenario file at '$scenarioPath' is not valid JSON: $_"
}

if (-not $scenarioObj.schemaVersion -or [int]$scenarioObj.schemaVersion -ne 1) {
    throw "Scenario schemaVersion must be 1 (found: $($scenarioObj.schemaVersion))."
}
if ([string]::IsNullOrWhiteSpace($scenarioObj.id)) {
    throw "Scenario missing required 'id' property."
}

$scenarioErrors=@(Test-OnlineScenario $scenarioObj)
if ($scenarioErrors.Count) { throw ($scenarioErrors -join '; ') }

# 4. Resolve Client Count
$clientCount = if ($Clients -gt 0) { $Clients } elseif ($scenarioObj.expectedPlayers) { [int]$scenarioObj.expectedPlayers } else { 2 }
if ($clientCount -lt 1 -or $clientCount -gt 10) {
    throw "Client count must be between 1 and 10 (requested: $clientCount)."
}

if ($ClientConfigs.Count -and $ClientConfigs.Count -ne $clientCount) { throw 'ClientConfigs must contain exactly one config per player.' }

# 5. Resolve Room Code (Strictly via NET_ROOM_CODE - never passed via argv/log)
$effectiveRoomCode = if ($RoomCode) { $RoomCode.Trim() } elseif ($env:NET_ROOM_CODE) { $env:NET_ROOM_CODE.Trim() } else { "" }
if ([string]::IsNullOrWhiteSpace($effectiveRoomCode)) {
    throw "NET_ROOM_CODE is required to run online test launcher."
}

# 6. Resolve Watchdog Timeout
$deadlineSec = if ($TimeoutSeconds -gt 0) {
    $TimeoutSeconds
} elseif ($scenarioObj.maxDurationSeconds) {
    [int]$scenarioObj.maxDurationSeconds + 10
} else {
    60
}

# 7. Prepare Output Directory
$outDir = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outDir) { throw 'Use a NEW OutputDirectory; do not overwrite previous evidence.' }
New-Item -ItemType Directory -Path $outDir | Out-Null

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " JungleMaze Online Readiness Test Launcher" -ForegroundColor Cyan
Write-Host " Scenario    : $($scenarioObj.id) (Clients: $clientCount, MaxDuration: ${deadlineSec}s)" -ForegroundColor Gray
Write-Host " Output Dir  : $outDir" -ForegroundColor Gray
Write-Host " Room Auth   : NET_ROOM_CODE env injected (masked from argv/logs)" -ForegroundColor Gray
Write-Host " Physical PCs: 1 (physicalTwoPc: NOT_TESTED)" -ForegroundColor Gray
Write-Host "=================================================================" -ForegroundColor Cyan

$trackedClients = [System.Collections.Generic.List[PSCustomObject]]::new()
$overallStatus = "FAIL"
$timedOut=$false
$launcherFailure=$null

try {
    # 8. Launch Clients (2..10, 640x360 windows, unique evidence and log paths)
    for ($i = 1; $i -le $clientCount; $i++) {
        $clientName = "Player$i"
        if ($ClientConfigs.Count) { $configPath=(Resolve-Path -LiteralPath $ClientConfigs[$i-1]).Path }
        $evidenceFile = Join-Path $outDir "evidence-client-$i.json"
        $logFile = Join-Path $outDir "unity-client-$i.log"

        if (Test-Path -LiteralPath $evidenceFile) {
            Remove-Item -LiteralPath $evidenceFile -Force -ErrorAction SilentlyContinue
        }

        # Spec CLI arguments: -onlineTest <scenario> -onlineEvidence <evidence> -clientConfig <config> -name <name>
        # Window configuration: 640x360 windowed
        $argList = "-screen-width 640 -screen-height 360 -screen-fullscreen 0 " +
                   "-onlineTest `"$scenarioPath`" " +
                   "-onlineEvidence `"$evidenceFile`" " +
                   "-clientConfig `"$configPath`" " +
                   "-name `"$clientName`" " +
                   "-logfile `"$logFile`""

        $psi = [System.Diagnostics.ProcessStartInfo]::new()
        if ($playerExe.EndsWith(".cmd", [System.StringComparison]::OrdinalIgnoreCase) -or
            $playerExe.EndsWith(".bat", [System.StringComparison]::OrdinalIgnoreCase)) {
            $psi.FileName = "$env:ComSpec"
            $psi.Arguments = "/c `"$playerExe`" $argList"
        } elseif ($playerExe.EndsWith(".ps1", [System.StringComparison]::OrdinalIgnoreCase)) {
            $psi.FileName = "powershell.exe"
            $psi.Arguments = "-ExecutionPolicy Bypass -File `"$playerExe`" $argList"
        } else {
            $psi.FileName = $playerExe
            $psi.Arguments = $argList
        }
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        # CRITICAL: Room code passed ONLY via NET_ROOM_CODE environment variable
        $psi.EnvironmentVariables["NET_ROOM_CODE"] = $effectiveRoomCode

        Write-Host "Launching client $i/$clientCount ($clientName)..." -ForegroundColor Yellow
        $proc = [System.Diagnostics.Process]::Start($psi)
        if ($null -eq $proc) {
            throw "Failed to start process for client $clientName"
        }

        $clientRecord = [PSCustomObject]@{
            Index = $i
            Name = $clientName
            Process = $proc
            StdoutTask = $proc.StandardOutput.ReadToEndAsync()
            StderrTask = $proc.StandardError.ReadToEndAsync()
            Pid = $proc.Id
            StartTime = $proc.StartTime
            EvidencePath = $evidenceFile
            LogPath = $logFile
            ExitCode = $null
            EvidenceStatus = "UNKNOWN"
            EvaluatedStatus = "UNKNOWN"
            Reason = $null
            SelectedTransport = $null
            FallbackReason = $null
            ConnectedSeconds = 0.0
            P50RttMs = 0.0
            P99RttMs = 0.0
            AssertionsPassed = 0
            AssertionsTotal = 0
        }
        $trackedClients.Add($clientRecord)

        if ($StaggerMs -gt 0 -and $i -lt $clientCount) {
            Start-Sleep -Milliseconds $StaggerMs
        }
    }

    Write-Host "All $clientCount clients launched. Monitoring execution (deadline: ${deadlineSec}s)..." -ForegroundColor Cyan

    # 9. Bounded Monitoring Loop
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $timedOut = $false

    while ($stopwatch.Elapsed.TotalSeconds -lt $deadlineSec) {
        Start-Sleep -Milliseconds 500

        $allExited = $true
        $allEvidenceCompleted = $true

        foreach ($c in $trackedClients) {
            if (-not $c.Process.HasExited) {
                $allExited = $false
            }
            if (-not (Test-Path -LiteralPath $c.EvidencePath)) {
                $allEvidenceCompleted = $false
            } else {
                try {
                    $jsonTest = Get-Content -LiteralPath $c.EvidencePath -Raw -Encoding utf8 | ConvertFrom-Json
                    if (-not $jsonTest.status -or $jsonTest.status -eq 'UNKNOWN') {
                        $allEvidenceCompleted = $false
                    }
                } catch {
                    $allEvidenceCompleted = $false
                }
            }
        }

        # If all processes exited, or all produced terminal evidence, exit wait loop
        if ($allExited) {
            # Allow a short grace period for file buffers to flush
            Start-Sleep -Milliseconds 1000
            break
        }
    }

    if ($stopwatch.Elapsed.TotalSeconds -ge $deadlineSec) {
        $timedOut = $true
        Write-Warning "Watchdog deadline of $deadlineSec seconds reached!"
    }

} catch {
    $launcherFailure='Launch/monitor failed: '+$_.Exception.GetType().Name
} finally {
    # 10. CLEANUP: Closes ONLY owned PID matching exact StartTime. Never kill unrelated Unity processes!
    Write-Host "Cleaning up owned client processes..." -ForegroundColor Gray
    foreach ($c in $trackedClients) {
        if ($null -ne $c.Process) {
            try {
                if (-not $c.Process.HasExited) {
                    $liveProc = [System.Diagnostics.Process]::GetProcessById($c.Pid)
                    # Verify PID ownership and exact launch timestamp
                    if ($liveProc.StartTime -eq $c.StartTime) {
                        Write-Host " Terminating owned process PID $($c.Pid) ($($c.Name))..." -ForegroundColor DarkGray
                        $liveProc.Kill()
                        $null = $liveProc.WaitForExit(3000)
                    }
                }
                $c.ExitCode = $c.Process.ExitCode
            } catch [System.ArgumentException] {
                # Process already closed
                try { $c.ExitCode = $c.Process.ExitCode } catch { $c.ExitCode = -1 }
            } catch {
                Write-Verbose "Error closing PID $($c.Pid): $_"
                $c.ExitCode = -1
            }
        }
    }
}

foreach ($c in $trackedClients) {
    if ($c.StdoutTask.IsCompleted) { [IO.File]::WriteAllText(($c.LogPath+'.startup.log'),$c.StdoutTask.GetAwaiter().GetResult()) }
    if ($c.StderrTask.IsCompleted) { [IO.File]::WriteAllText(($c.LogPath+'.stderr.log'),$c.StderrTask.GetAwaiter().GetResult()) }
}

# 11. Evidence Verification & Status Classification
# Never reinterpret process exit code 0 as PASS without valid evidence file and matching assertions!
$passCount = 0
foreach ($c in $trackedClients) {
    if (-not (Test-Path -LiteralPath $c.EvidencePath)) { $c.EvaluatedStatus='FAIL';$c.Reason='Missing evidence after player exit/deadline';continue }
    try {
        $e=Get-Content -LiteralPath $c.EvidencePath -Raw -Encoding utf8 | ConvertFrom-Json
        $v=Test-OnlineEvidence $e $scenarioObj $c.ExitCode
        $c.EvidenceStatus=Get-OnlineField $e status 'FAIL'
        $c.EvaluatedStatus=$v.status;$c.Reason=$v.reason
        $c.SelectedTransport=Get-OnlineField $e selectedTransport ''
        $c.FallbackReason=Get-OnlineField $e fallbackReason ''
        $m=Get-OnlineField $e metrics
        $c.ConnectedSeconds=[double](Get-OnlineField $m connectedSeconds 0)
        $c.P50RttMs=[double](Get-OnlineField $m rttP50Ms 0);$c.P99RttMs=[double](Get-OnlineField $m rttP99Ms 0)
        $c.AssertionsPassed=$v.passedAssertions;$c.AssertionsTotal=$v.requiredAssertions
        if ($v.status -eq 'PASS') { $passCount++ }
    } catch { $c.EvaluatedStatus='FAIL';$c.Reason='Malformed/incomplete evidence' }
}
$overallStatus=if ($launcherFailure -or $trackedClients.Count -ne $clientCount -or @($trackedClients | Where-Object {$_.EvaluatedStatus -eq 'FAIL'}).Count) {'FAIL'} elseif (@($trackedClients | Where-Object {$_.EvaluatedStatus -eq 'INVALID'}).Count) {'INVALID'} elseif (@($trackedClients | Where-Object {$_.EvaluatedStatus -eq 'BLOCKED'}).Count) {'BLOCKED'} else {'PASS'}

# 12. Generate report.json ({ status, clients: [...] })
$reportClients = @()
foreach ($c in $trackedClients) {
    $reportClients += [pscustomobject][ordered]@{
        id = $c.Name
        index = $c.Index
        pid = $c.Pid
        exitCode = $c.ExitCode
        status = $c.EvaluatedStatus
        evidenceStatus = $c.EvidenceStatus
        selectedTransport = $c.SelectedTransport
        fallbackReason = $c.FallbackReason
        connectedSeconds = [Math]::Round($c.ConnectedSeconds, 2)
        p50RttMs = [Math]::Round($c.P50RttMs, 2)
        p99RttMs = [Math]::Round($c.P99RttMs, 2)
        assertionsPassed = $c.AssertionsPassed
        assertionsTotal = $c.AssertionsTotal
        reason = $c.Reason
        evidenceFile = [System.IO.Path]::GetFileName($c.EvidencePath)
        logFile = [System.IO.Path]::GetFileName($c.LogPath)
    }
}

$reportObject = [ordered]@{
    status = $overallStatus
    scenarioId = $scenarioObj.id
    schemaVersion = 1
    timestampUtc = [DateTime]::UtcNow.ToString("o")
    reason = $launcherFailure
    playerSha256 = (Get-FileHash -LiteralPath $playerExe -Algorithm SHA256).Hash.ToLowerInvariant()
    playerFingerprint = Get-OnlineBuildFingerprint $playerExe
    clientCount = $clientCount
    passedCount = $passCount
    physicalDeviceCount = 1
    physicalTwoPc = "NOT_TESTED"
    clients = $reportClients
}

$reportJsonPath = Join-Path $outDir "report.json"
$reportObject | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportJsonPath -Encoding utf8

# Also export summary.csv for tabular aggregation
$csvRows = @()
foreach ($c in $trackedClients) {
    $csvRows += [pscustomobject][ordered]@{
        ClientId = $c.Index
        Name = $c.Name
        Status = $c.EvaluatedStatus
        SelectedTransport = $c.SelectedTransport
        FallbackReason = $c.FallbackReason
        ConnectedSeconds = [Math]::Round($c.ConnectedSeconds, 2)
        P50RttMs = [Math]::Round($c.P50RttMs, 2)
        P99RttMs = [Math]::Round($c.P99RttMs, 2)
        ExitCode = $c.ExitCode
        AssertionsPassed = $c.AssertionsPassed
        AssertionsTotal = $c.AssertionsTotal
        PhysicalDeviceCount = 1
        PhysicalTwoPc = "NOT_TESTED"
        Reason = $c.Reason
    }
}
$csvPath = Join-Path $outDir "summary.csv"
$csvRows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding utf8

# 13. Console Summary Table
Write-Host "`n========================= RUN SUMMARY =========================" -ForegroundColor Cyan
Write-Host "Overall Status: $overallStatus ($passCount/$clientCount clients passed)" -ForegroundColor $(if ($overallStatus -eq 'PASS') { 'Green' } else { 'Red' })
Write-Host "PhysicalTwoPc : NOT_TESTED (single physical device)" -ForegroundColor Yellow
Write-Host "Report JSON   : $reportJsonPath" -ForegroundColor Gray
Write-Host "Summary CSV   : $csvPath" -ForegroundColor Gray
Write-Host "---------------------------------------------------------------" -ForegroundColor Gray

$reportClients | Select-Object id, status, selectedTransport, connectedSeconds, p50RttMs, exitCode, reason | Format-Table -AutoSize | Out-Host

if ($overallStatus -ne 'PASS') {
    exit 1
}
exit 0
