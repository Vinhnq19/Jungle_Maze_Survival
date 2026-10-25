Set-StrictMode -Version Latest
function Get-OnlineField($Object,[string]$Name,$Default=$null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) { if ($Object.Contains($Name)) { return $Object[$Name] };return $Default }
    $p=$Object.PSObject.Properties[$Name];if ($null -eq $p) { return $Default };return $p.Value
}
function Test-OnlineScenario($Scenario) {
    $errors=[Collections.Generic.List[string]]::new()
    $known=@('$schema','description','schemaVersion','id','maxDurationSeconds','minConnectedSeconds','expectedPlayers','requireMovement','requireZone','requireHpDamage','requireResult','requireReplay','requireHistory','minInputHz','minSnapshotHz','maxPongTimeoutRatio','driver','extraAssertions')
    foreach ($p in $Scenario.PSObject.Properties) { if ($p.Name -notin $known) { $errors.Add('Unknown scenario field: '+$p.Name) } }
    if ((Get-OnlineField $Scenario schemaVersion 0) -ne 1) { $errors.Add('schemaVersion must be1') }
    if ([string]::IsNullOrWhiteSpace((Get-OnlineField $Scenario id ''))) { $errors.Add('id required') }
    $players=Get-OnlineField $Scenario expectedPlayers 2
    if ($players -lt 1 -or $players -gt 10) { $errors.Add('expectedPlayers must be1..10') }
    $max=[double](Get-OnlineField $Scenario maxDurationSeconds 120);$min=[double](Get-OnlineField $Scenario minConnectedSeconds 5)
    if ([double]::IsNaN($max) -or [double]::IsInfinity($max) -or $max -lt 5 -or $max -gt 900 -or $min -lt 0 -or $min -ge $max) { $errors.Add('Invalid duration/deadline') }
    $hz=[double](Get-OnlineField $Scenario minInputHz 28.5)
    if ([double]::IsNaN($hz) -or [double]::IsInfinity($hz) -or $hz -lt 0 -or $hz -gt 30) { $errors.Add('Invalid minInputHz') }
    if ((Get-OnlineField $Scenario requireReplay $false) -and -not (Get-OnlineField $Scenario requireResult $false)) { $errors.Add('Replay requires result') }
    return $errors.ToArray()
}
function Get-OnlineRequiredAssertions($Scenario) {
    $ids=[Collections.Generic.List[string]]::new();$ids.Add('joined')
    if ((Get-OnlineField $Scenario expectedPlayers 2) -gt 1) { $ids.Add('playersVisible') }
    foreach ($pair in @(@('requireMovement','movement',$true),@('requireZone','zone',$true),@('requireHpDamage','hpDamage',$false),@('requireResult','result',$false),@('requireReplay','replay',$false),@('requireHistory','history',$false))) {
        if (Get-OnlineField $Scenario $pair[0] $pair[2]) { $ids.Add($pair[1]) }
    }
    foreach ($name in @(Get-OnlineField $Scenario extraAssertions @())) { $ids.Add([string]$name) }
    return $ids.ToArray()
}
function Test-OnlineEvidence($Evidence,$Scenario,[int]$ExitCode) {
    $result=[ordered]@{status='FAIL';reason='';requiredAssertions=0;passedAssertions=0}
    if ($ExitCode -ne 0 -and (Get-OnlineField $Evidence status '') -notin @('INVALID','BLOCKED')) { $result.reason='Player crashed or exited with error';return [pscustomobject]$result }
    if ((Get-OnlineField $Evidence schemaVersion 0) -ne 1 -or (Get-OnlineField $Evidence scenarioId '') -ne $Scenario.id) { $result.reason='Missing/mismatched evidence schema or scenario';return [pscustomobject]$result }
    $status=Get-OnlineField $Evidence status ''
    if ($status -ne 'PASS') { $result.status=if ($status -in @('FAIL','INVALID','BLOCKED')) {$status} else {'FAIL'};$result.reason='Player reported '+$result.status;return [pscustomobject]$result }
    $metrics=Get-OnlineField $Evidence metrics
    if ($null -eq $metrics -or [double](Get-OnlineField $metrics connectedSeconds -1) -lt [double](Get-OnlineField $Scenario minConnectedSeconds 5)) { $result.reason='Missing/insufficient connected time';return [pscustomobject]$result }
    $rate=Get-OnlineField $metrics inputHz;$minRate=[double](Get-OnlineField $Scenario minInputHz 28.5)
    if ($minRate -gt 0 -and ($null -eq $rate -or [double]::IsNaN([double]$rate) -or [double]::IsInfinity([double]$rate) -or [double]$rate -lt $minRate)) { $result.status='INVALID';$result.reason='Generator input rate absent/below requested rate';return [pscustomobject]$result }
    $snapshotMin=[double](Get-OnlineField $Scenario minSnapshotHz 0)
    if ($snapshotMin -gt 0 -and [double](Get-OnlineField $metrics snapshotHz -1) -lt $snapshotMin) { $result.reason='Snapshot rate below requested rate';return [pscustomobject]$result }
    $pings=[double](Get-OnlineField $metrics pings 0);$timeouts=[double](Get-OnlineField $metrics pongTimeouts 0)
    if ($pings -gt 0 -and $timeouts -gt 0 -and $timeouts/$pings -ge [double](Get-OnlineField $Scenario maxPongTimeoutRatio 0.01)) { $result.reason='Pong timeout threshold exceeded';return [pscustomobject]$result }
    $assertions=@{};foreach ($a in @(Get-OnlineField $Evidence assertions @())) {
        $id=[string](Get-OnlineField $a id '')
        if (-not $id -or $assertions.ContainsKey($id)) { $result.reason='Malformed/duplicate assertion';return [pscustomobject]$result }
        $assertions[$id]=Get-OnlineField $a status ''
    }
    foreach ($id in @(Get-OnlineRequiredAssertions $Scenario)) {
        $result.requiredAssertions++
        if (-not $assertions.ContainsKey($id) -or $assertions[$id] -ne 'PASS') { $result.reason='Required assertion missing/failed: '+$id;return [pscustomobject]$result }
        $result.passedAssertions++
    }
    $result.status='PASS';$result.reason='Required observations and rates verified';return [pscustomobject]$result
}
function Get-OnlineBuildFingerprint([string]$Player) {
    $exe=(Resolve-Path -LiteralPath $Player).Path
    $root=[IO.Path]::GetDirectoryName($exe)
    $files=[Collections.Generic.List[string]]::new();$files.Add($exe)
    foreach ($name in @('UnityPlayer.dll','UnityCrashHandler64.exe')) { $f=Join-Path $root $name;if (Test-Path -LiteralPath $f) {$files.Add($f)} }
    $data=Join-Path $root (([IO.Path]::GetFileNameWithoutExtension($exe))+'_Data')
    if (Test-Path -LiteralPath $data) { foreach ($f in Get-ChildItem -LiteralPath $data -File -Recurse) {$files.Add($f.FullName)} }
    $text=[Text.StringBuilder]::new()
    # Ordinal ordering is identical in Windows PowerShell5 and PowerShell7.
    $files.Sort([StringComparer]::Ordinal)
    foreach ($f in $files) { $relative=$f.Substring($root.Length+1).Replace('\','/');$hash=(Get-FileHash -LiteralPath $f -Algorithm SHA256).Hash.ToLowerInvariant();$null=$text.Append($relative+':'+$hash+"`n") }
    $algorithm=[Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($text.ToString()))).Replace('-','').ToLowerInvariant() } finally {$algorithm.Dispose()}
}
Export-ModuleMember -Function Get-OnlineField,Test-OnlineScenario,Get-OnlineRequiredAssertions,Test-OnlineEvidence,Get-OnlineBuildFingerprint
