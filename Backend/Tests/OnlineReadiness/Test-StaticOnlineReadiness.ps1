param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
Push-Location $repo
try {
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $repo ('.utmp/online-20261024/static-'+[Guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
Import-Module (Join-Path $repo 'Backend/Tests/OnlineReadiness/OnlineReadiness.psm1') -Force
$checks=@()
function Check([bool]$Ok,[string]$Name) { $script:checks+=@{name=$Name;passed=$Ok};if (-not $Ok) { throw "FAILED $Name" };Write-Output "PASS $Name" }
foreach ($file in Get-ChildItem Backend/Deploy -Filter '*.ps1' -Recurse) {
    $errors=$null;$tokens=$null;$null=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
    Check ($errors.Count -eq 0) ('Parse '+$file.Name)
}
foreach ($f in Get-ChildItem Backend/Tests/OnlineReadiness/scenarios -Filter '*.json') {
    $s=Get-Content -LiteralPath $f.FullName -Raw | ConvertFrom-Json
    Check (@(Test-OnlineScenario $s).Count -eq 0) ('Scenario '+$s.id)
}
$scenario=[pscustomobject]@{schemaVersion=1;id='evidence-test';maxDurationSeconds=10;minConnectedSeconds=5;expectedPlayers=2;minInputHz=28.5;requireHistory=$true}
$asserts=@('joined','playersVisible','movement','zone','history') | ForEach-Object {[pscustomobject]@{id=$_;status='PASS';detail='Observed'}}
$e=[pscustomobject]@{schemaVersion=1;scenarioId='evidence-test';status='PASS';metrics=[pscustomobject]@{connectedSeconds=6;inputHz=30};assertions=$asserts}
Check ((Test-OnlineEvidence $e $scenario 0).status -eq 'PASS') 'Evidence matches required assertions'
$e.metrics.inputHz=0;Check ((Test-OnlineEvidence $e $scenario 0).status -eq 'INVALID') 'Zero generator rate cannot pass';$e.metrics.inputHz=30
Check ((Test-OnlineEvidence $e $scenario 3).status -eq 'FAIL') 'Crash with PASS JSON cannot pass'
$e.scenarioId='wrong';Check ((Test-OnlineEvidence $e $scenario 0).status -eq 'FAIL') 'Stale scenario evidence rejected';$e.scenarioId='evidence-test'
$e.assertions=@($asserts | Where-Object {$_.id -ne 'history'});Check ((Test-OnlineEvidence $e $scenario 0).status -eq 'FAIL') 'Missing required history rejected'
$fixture=Join-Path $OutputDirectory 'fake-player.exe'
$source=@'
using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
class FakePlayer {
 static int Main(string[] args) {
  string output=null;for(int i=0;i+1<args.Length;i++)if(args[i]=="-onlineEvidence")output=args[i+1];
  if(Environment.GetEnvironmentVariable("NET_ROOM_CODE")!="fixture-room")return 9;
  string kind=Environment.GetEnvironmentVariable("ONLINE_FAKE_CASE")??"pass";
  if(kind=="missing")return 0;
  var assertions=new List<object>();foreach(string id in new[]{"joined","playersVisible","movement","zone","history"})assertions.Add(new {id=id,status="PASS",detail="Fixture observation"});
  var e=new {schemaVersion=1,scenarioId=kind=="wrong"?"wrong":"evidence-test",status="PASS",selectedTransport="Udp",fallbackReason="",metrics=new{connectedSeconds=6,inputHz=kind=="zero"?0:30,rttP50Ms=1,rttP99Ms=2},assertions=assertions};
  File.WriteAllText(output,new JavaScriptSerializer().Serialize(e));
  System.Threading.Thread.Sleep(150);return kind=="crash"?3:0;
 }
}
'@
Add-Type -TypeDefinition $source -ReferencedAssemblies 'System.Web.Extensions.dll' -OutputAssembly $fixture -OutputType ConsoleApplication
$config=Join-Path $OutputDirectory 'client.json';'{}' | Set-Content -LiteralPath $config -Encoding utf8
$scenarioPath=Join-Path $OutputDirectory 'scenario.json';$scenario | ConvertTo-Json | Set-Content -LiteralPath $scenarioPath -Encoding utf8
$previousCode=$env:NET_ROOM_CODE;$previousCase=$env:ONLINE_FAKE_CASE
try {
    $env:NET_ROOM_CODE='fixture-room'
    foreach ($kind in @('pass','zero','wrong','crash','missing')) {
        $env:ONLINE_FAKE_CASE=$kind;$out=Join-Path $OutputDirectory $kind
        & powershell -NoProfile -File Backend/Deploy/test-unity-online.ps1 -Player $fixture -Config $config -Scenario $scenarioPath -Clients 1 -OutputDirectory $out *> (Join-Path $OutputDirectory ($kind+'.log'))
        $r=Get-Content -LiteralPath (Join-Path $out 'report.json') -Raw | ConvertFrom-Json
        $expected=if ($kind -eq 'pass') {'PASS'} elseif ($kind -eq 'zero') {'INVALID'} else {'FAIL'}
        Check ($r.status -eq $expected) ('Actual launcher fixture '+$kind+' -> '+$expected)
        Check ($r.physicalTwoPc -eq 'NOT_TESTED') ('No fabricated two-PC evidence '+$kind)
    }
} finally { $env:NET_ROOM_CODE=$previousCode;$env:ONLINE_FAKE_CASE=$previousCase }
$checks | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'checks.json') -Encoding utf8
Write-Output ('PASS '+$checks.Count+' static and actual launcher checks. Evidence: '+$OutputDirectory)

} finally { Pop-Location }
