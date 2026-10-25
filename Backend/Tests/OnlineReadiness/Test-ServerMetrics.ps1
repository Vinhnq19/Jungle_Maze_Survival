param([Parameter(Mandatory=$true)][string]$LogPath,[Parameter(Mandatory=$true)][double]$MemoryPercent,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$rows=@()
foreach ($line in Get-Content -LiteralPath $LogPath) {
    if ($line -notmatch '\[metrics\]') { continue }
    $v=@{};foreach ($m in [regex]::Matches($line,'(\w+)=([^\s]+)')) {$v[$m.Groups[1].Value]=$m.Groups[2].Value}
    if (-not $v.ContainsKey('active') -or [int]$v.active -lt 1) { continue }
    $culture=[Globalization.CultureInfo]::InvariantCulture
    $rows+= [pscustomobject]@{tick=[double]::Parse($v.ticksPerSecond,$culture);cpu=[double]::Parse($v.cpuOneCorePercent,$culture);dropped=[int]$v.droppedTicks}
}
$status='FAIL';$reason='Missing active-player metrics';$tick=0;$cpu=0;$dropped=0
if ($rows.Count) {
    $tick=($rows|Measure-Object tick -Average).Average;$cpu=($rows|Measure-Object cpu -Maximum).Maximum;$dropped=($rows|Measure-Object dropped -Sum).Sum
    if ($tick -ge 29 -and $tick -le 31 -and $cpu -le 85 -and $MemoryPercent -le 80 -and $dropped -eq 0) {$status='PASS';$reason='Active-player tick/CPU/RAM thresholds satisfied'}else{$reason='Tick/CPU/RAM/dropped-tick threshold failed'}
}
[ordered]@{status=$status;reason=$reason;activeSamples=$rows.Count;tickAverageHz=$tick;cpuPeakOneCorePercent=$cpu;memoryPercent=$MemoryPercent;droppedTicks=$dropped;memorySampling='Sampled peak from bounded Docker stats collector plus final sample'} | ConvertTo-Json | Set-Content -LiteralPath $OutputPath -Encoding utf8
if ($status -ne 'PASS') { throw $reason }
