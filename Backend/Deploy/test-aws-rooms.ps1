param(
    [string]$AwsCli = 'D:/AWS/ConnectionSetup/AWSCLI-portable/aws.exe',
    [string]$Profile = 'junglemaze-admin',
    [string]$Region = 'ap-southeast-2',
    [string]$InstanceId = 'i-0e617f8072fbb9878',
    [string]$SshAlias = 'junglemaze',
    [string]$Python = 'python',
    [string]$Release = '',
    [string]$DeltaRelease = '',
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [switch]$PreflightOnly,
    [switch]$ReuseDeployedRelease,
    [switch]$GeneratorOnServer,
    [string]$CapacityAfter = '',
    [switch]$Phase1Connectivity,
    [string]$VpnIp = '',
    [string]$LocalConfig = '',
    # Only after the owner explicitly reserves an already Running instance for this test.
    [switch]$UseRunningInstance,
    [switch]$DualTransport,
    [switch]$WssReconnectOnly,
    [ValidateRange(8,20)][int]$BudgetMinutes = 20,
    [ValidateRange(10,30)][int]$HardStopMinutes = 30
)
$ErrorActionPreference = 'Stop'
$taskBackend = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskLifecycle = [ordered]@{ instanceId=$InstanceId; region=$Region; startedByThisRun=$false; stoppedConfirmed=$false; events=@() }
$taskWatchdog = $null
$taskSleepHeld = $false
function Aws([string[]]$Arguments) {
    $raw = & $AwsCli @Arguments --profile $Profile --region $Region --output json --no-cli-pager --cli-connect-timeout 10 --cli-read-timeout 30 2>&1
    if ($LASTEXITCODE -ne 0) { throw "AWS $($Arguments[0])/$($Arguments[1]) failed: $raw" }
    if ($raw) { return ($raw -join [Environment]::NewLine | ConvertFrom-Json) }
}
function Event([string]$Message) {
    Write-Output $Message
    $taskLifecycle.events += @{ utc=[DateTime]::UtcNow.ToString('o'); message=$Message }
}
function Save-Lifecycle {
    $taskLifecycle | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'lifecycle.json') -Encoding utf8
}
function State { (Aws @('ec2','describe-instances','--instance-ids',$InstanceId)).Reservations[0].Instances[0] }
function Bounded([string]$Exe,[string]$Arguments,[int]$Seconds) {
    $p = Start-Process -FilePath $Exe -ArgumentList $Arguments -PassThru -NoNewWindow
    $null = $p.Handle
    if (-not $p.WaitForExit($Seconds*1000)) { & taskkill /PID $p.Id /T /F | Out-Null; throw "Timed out: $Exe" }
    $p.Refresh()
    if ($p.ExitCode -ne 0) { throw "Process failed: $Exe ($($p.ExitCode))" }
}
function Probe-PublicHttps([string]$Ip) {
    # Public HTTPS probe proves a real PC -> EC2 path. SSM is not a substitute here.
    $healthy=$false
    for ($probe=0; $probe -lt 3; $probe++) {
        try {
            Bounded 'curl.exe' ("--fail --silent --show-error --connect-timeout 5 --max-time 15 --resolve junglemaze.duckdns.org:443:{0} https://junglemaze.duckdns.org/health" -f $Ip) 20
            $healthy=$true; break
        } catch { if ($probe -lt 2) { Start-Sleep -Seconds 3 } }
    }
    if (-not $healthy) { throw 'Public TLS/health unreachable; abort before upload to save credits.' }
    Event 'Public TLS/health verified. Test client uses explicit per-process IP pin; system DNS/hosts remain unchanged.'
}
function Restart-Guarded {
    Aws @('ec2','stop-instances','--instance-ids',$InstanceId) | Out-Null
    $by=[DateTime]::UtcNow.AddMinutes(3)
    do {
        if ((State).State.Name -eq 'stopped') { break }
        if ([DateTime]::UtcNow -ge $by) { throw 'Restart Stop timeout.' }
        Start-Sleep -Seconds 5
    } while ($true)
    if ([DateTime]::UtcNow.AddMinutes(4) -ge [DateTime]::Parse($env:AWS_ROOM_DEADLINE_UTC).ToUniversalTime()) { throw 'Not enough budget for rejoin.' }
    Aws @('ec2','start-instances','--instance-ids',$InstanceId) | Out-Null
    $by=[DateTime]::UtcNow.AddMinutes(3)
    do {
        $current=State
        $ssm=Aws @('ssm','describe-instance-information','--filters',"Key=InstanceIds,Values=$InstanceId")
        if ($current.State.Name -eq 'running' -and $ssm.InstanceInformationList.Count -gt 0 -and $ssm.InstanceInformationList[0].PingStatus -eq 'Online') { break }
        if ([DateTime]::UtcNow -ge $by) { throw 'Restart SSM timeout.' }
        Start-Sleep -Seconds 5
    } while ($true)
    $timer=Aws @('ssm','send-command','--instance-ids',$InstanceId,'--document-name','AWS-RunShellScript','--parameters',('file://'+$paramFile))
    $ok=$false
    for ($i=0;$i -lt 12;$i++) {
        Start-Sleep -Seconds 2
        try { $inv=Aws @('ssm','get-command-invocation','--command-id',$timer.Command.CommandId,'--instance-id',$InstanceId) } catch { continue }
        if ($inv.Status -eq 'Success') { $ok=$true;break }
    }
    if (-not $ok) { throw 'Restart stop timer could not be armed.' }
    return $current.PublicIpAddress
}
try {
    $identity = Aws @('sts','get-caller-identity')
    $instance = State
    $taskLifecycle.account = $identity.Account
    $taskLifecycle.instanceType = $instance.InstanceType
    $taskLifecycle.initialState = $instance.State.Name
    if ($UseRunningInstance -and -not ($Phase1Connectivity -or $DualTransport)) { throw 'UseRunningInstance is restricted to an explicitly reserved connectivity test.' }
    if ($DualTransport -and ($Phase1Connectivity -or $GeneratorOnServer -or $CapacityAfter)) { throw 'DualTransport requires real PC and its own test path.' }
    if ($WssReconnectOnly -and -not $DualTransport) { throw 'WssReconnectOnly requires DualTransport.' }
    if ($DualTransport -and $BudgetMinutes -ge $HardStopMinutes) { throw 'BudgetMinutes must leave time before HardStopMinutes.' }
    if ($instance.State.Name -ne 'stopped' -and -not ($UseRunningInstance -and $instance.State.Name -eq 'running')) { throw 'Require an exclusively reserved, Stopped instance; refusing to interrupt an existing session.' }
    if ($UseRunningInstance -and -not $PreflightOnly -and $instance.State.Name -eq 'running') {
        # Stop was explicitly authorized even if a later preflight/fixture step fails.
        $taskLifecycle.adoptedRunningInstance=$true
        $taskLifecycle.adoptedUtc=[DateTime]::UtcNow.ToString('o')
        $taskLifecycle.initialLaunchUtc=$instance.LaunchTime
        Save-Lifecycle
    }
    if ($instance.Architecture -ne 'x86_64' -or $instance.RootDeviceType -ne 'ebs') { throw 'Require x86_64 with persistent EBS root.' }
    $behavior = Aws @('ec2','describe-instance-attribute','--instance-id',$InstanceId,'--attribute','instanceInitiatedShutdownBehavior')
    if ($behavior.InstanceInitiatedShutdownBehavior.Value -ne 'stop') { throw 'OS shutdown must STOP, never terminate.' }
    $credit = Aws @('ec2','describe-instance-credit-specifications','--instance-ids',$InstanceId)
    if ($credit.InstanceCreditSpecifications[0].CpuCredits -ne 'standard') { throw 'Set CPU credits to Standard before the paid test.' }
    # DryRun proves permissions without starting/stopping anything.
    foreach ($verb in @('start-instances','stop-instances')) {
        $ErrorActionPreference = 'Continue'
        try { $dry = & $AwsCli ec2 $verb --instance-ids $InstanceId --dry-run --profile $Profile --region $Region --no-cli-pager 2>&1 | Out-String }
        finally { $ErrorActionPreference = 'Stop' }
        if ($dry -notmatch 'DryRunOperation') { throw "Missing permission for $verb" }
    }
    if ($Phase1Connectivity -or $WssReconnectOnly) {
        if ($GeneratorOnServer -or $CapacityAfter) { throw 'Phase1Connectivity requires real PC sockets; capacity continuation/generator-on-server are not allowed.' }
        if (-not (Test-Path (Join-Path $taskBackend 'NetSmokeClient/bin/Release/net8.0/NetSmokeClient.dll'))) { throw 'Build Release NetSmokeClient locally before starting EC2.' }
    } else {
        if (-not $Release) { throw 'Provide -Release for deployment/capacity tests.' }
        & $Python -X utf8 (Join-Path $taskBackend 'Tests/verify_aws_release.py') $Release --apps-only
        if ($LASTEXITCODE -ne 0) { throw 'Build/verify the app-only release locally first.' }
        if ($DeltaRelease) {
            if (-not $DualTransport) { throw 'DeltaRelease requires DualTransport.' }
            $DeltaRelease=[IO.Path]::GetFullPath($DeltaRelease)
            & $Python -X utf8 (Join-Path $taskBackend 'Tools/aws_image_delta.py') verify --release $Release --archive $DeltaRelease
            if ($LASTEXITCODE -ne 0) { throw 'Verify delta locally before starting EC2.' }
        }
    }
    if ($CapacityAfter) {
        $previous = Get-Content -LiteralPath $CapacityAfter -Raw | ConvertFrom-Json
        if (-not $GeneratorOnServer -or -not $previous.generatorOnServer -or
            @($previous.checks | Where-Object {-not $_.passed}).Count -gt 0 -or
            @($previous.stages | Where-Object {$_.clients -eq 10 -and $_.status -eq 'PASS'}).Count -eq 0) {
            throw 'Capacity continuation requires previously successful real AWS security and 10-client checks.'
        }
    }
    if ($GeneratorOnServer -and -not (Test-Path -LiteralPath (Join-Path $taskBackend 'out/aws-room-test/smoke-client.tar.gz'))) {
        throw 'Publish Linux NetSmokeClient and pack out/aws-room-test/smoke-client.tar.gz BEFORE starting EC2.'
    }
    Event ('Preflight passed: '+$instance.State.Name+', EBS, x86_64, shutdown=stop, CPU Standard, Start/Stop allowed.')
    if ($PreflightOnly) { return }
    # Prepare reusable fixture credentials outside the repository; restrict ACL before use.
    $fixtureDirectory = Join-Path $env:LOCALAPPDATA 'JungleMaze'
    New-Item -ItemType Directory -Path $fixtureDirectory -Force | Out-Null
    $fixturePath = Join-Path $fixtureDirectory 'aws-room-fixtures.json'
    if (-not (Test-Path -LiteralPath $fixturePath)) {
        $fixtures = foreach ($suffix in @('A','B','C')) {
            @{ username=('awsbench'+[Guid]::NewGuid().ToString('N').Substring(0,10)+$suffix); password=([Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')) }
        }
        $fixtures | ConvertTo-Json | Set-Content -LiteralPath $fixturePath -Encoding utf8
    }
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
    # icacls changes DACL only; Set-Acl with a new FileSecurity can request SeSecurityPrivilege.
    & icacls $fixturePath /inheritance:r /grant:r ("*"+$sid.Value+':F') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not protect private fixture credentials.' }
    if (@((Get-Acl -LiteralPath $fixturePath).Access | Where-Object {
        $_.AccessControlType -eq 'Allow' -and $_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value -ne $sid.Value
    }).Count -gt 0) { throw 'Private fixture file has unexpected access rules.' }
    $taskFixtures = Get-Content -LiteralPath $fixturePath -Raw | ConvertFrom-Json
    if (@($taskFixtures).Count -ne 3 -or @($taskFixtures | Where-Object { -not $_.username -or -not $_.password }).Count -gt 0) {
        throw 'Invalid private fixture file; require three username/password records.'
    }
    $env:AWS_ROOM_FIXTURES = $fixturePath
    # This child survives this shell. Independent EC2 timer is added when SSM comes online.
    # Hold automatic idle Sleep only during this test; explicit Sleep/network changes
    # remain outside the tool's control. The EC2 timer still works without the PC.
    if (-not ('JungleMaze.TestPower' -as [type])) {
        Add-Type -TypeDefinition 'namespace JungleMaze { public static class TestPower { [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint flags); public static bool Hold() { return SetThreadExecutionState(0x80000001)!=0; } public static void Release() { SetThreadExecutionState(0x80000000); } } }'
    }
    $taskSleepHeld=[JungleMaze.TestPower]::Hold()
    $taskLifecycle.idleSleepPrevented=$taskSleepHeld
    $taskDeadline = [DateTime]::UtcNow.AddMinutes($HardStopMinutes)
    $taskLifecycle.hardStopUtc = $taskDeadline.ToString('o')
    $guard = @(
        ('$end = [DateTime]::Parse(''{0}'').ToUniversalTime()' -f $taskDeadline.ToString('o')),
        'while ([DateTime]::UtcNow -lt $end) { Start-Sleep -Seconds 5 }',
        ('& ''{0}'' ec2 stop-instances --instance-ids ''{1}'' --profile ''{2}'' --region ''{3}'' --no-cli-pager --cli-connect-timeout 10 --cli-read-timeout 30 *> ''{4}/watchdog.log''' -f $AwsCli,$InstanceId,$Profile,$Region,$OutputDirectory)
    ) -join [Environment]::NewLine
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($guard))
    $taskWatchdog = Start-Process powershell -WindowStyle Hidden -PassThru -ArgumentList "-NoProfile -EncodedCommand $encoded"
    # Mark ownership before request: even a lost Start response must lead to Stop.
    $taskLifecycle.adoptedRunningInstance = $UseRunningInstance -and $instance.State.Name -eq 'running'
    $taskLifecycle.startedByThisRun = -not $taskLifecycle.adoptedRunningInstance
    Save-Lifecycle
    $taskLifecycle.startRequestedUtc = [DateTime]::UtcNow.ToString('o')
    if ($taskLifecycle.adoptedRunningInstance) {
        $taskLifecycle.adoptedUtc = $taskLifecycle.startRequestedUtc
        $taskLifecycle.initialLaunchUtc = $instance.LaunchTime
        Event 'Explicitly reserved Running instance adopted; no Start requested. Stop at end is authorized.'
    } else {
        Aws @('ec2','start-instances','--instance-ids',$InstanceId) | Out-Null
        Event ("EC2 start requested. Hard deadline: $HardStopMinutes minutes including startup/deployment.")
    }
    Save-Lifecycle
    $readyBy = [DateTime]::UtcNow.AddMinutes(3)
    do {
        $instance = State
        $ssm = Aws @('ssm','describe-instance-information','--filters',"Key=InstanceIds,Values=$InstanceId")
        if ($instance.State.Name -eq 'running' -and $ssm.InstanceInformationList.Count -gt 0 -and $ssm.InstanceInformationList[0].PingStatus -eq 'Online') { break }
        if ([DateTime]::UtcNow -ge $readyBy) { throw 'SSM not ready within 3 minutes; abort to save credits.' }
        Start-Sleep -Seconds 5
    } while ($true)
    $taskLifecycle.publicIp = $instance.PublicIpAddress
    $stamp = $taskDeadline.ToString('yyyy-MM-dd HH:mm:ss') + ' UTC'
    $paramFile = Join-Path $OutputDirectory 'stop-timer-parameters.json'
    @{ commands=@("systemd-run --quiet --unit=junglemaze-test-stop --on-calendar='$stamp' /usr/sbin/shutdown -h now") } |
        ConvertTo-Json -Compress | Set-Content -LiteralPath $paramFile -Encoding ascii
    $timer = Aws @('ssm','send-command','--instance-ids',$InstanceId,'--document-name','AWS-RunShellScript','--parameters',('file://'+$paramFile))
    $timerOk = $false
    for ($i=0; $i -lt 12; $i++) {
        Start-Sleep -Seconds 2
        try { $inv = Aws @('ssm','get-command-invocation','--command-id',$timer.Command.CommandId,'--instance-id',$InstanceId) } catch { continue }
        if ($inv.Status -eq 'Success') { $timerOk=$true; break }
        if ($inv.Status -in @('Failed','TimedOut','Cancelled')) { break }
    }
    if (-not $timerOk) { throw 'Could not arm independent EC2 shutdown timer.' }
    Event 'SSM Online; independent server stop timer armed.'
    if ($DualTransport) { Probe-PublicHttps $instance.PublicIpAddress }
    if ($WssReconnectOnly) {
        # Short independent rejoin after the previous guarded run has already Stopped.
        # No upload, deployment, capacity load or second restart in this branch.
        $env:AWS_ROOM_DEADLINE_UTC=[DateTime]::Parse($taskLifecycle.startRequestedUtc).ToUniversalTime().AddMinutes($BudgetMinutes).ToString('o')
        & $Python -X utf8 (Join-Path $taskBackend 'Tools/room_benchmark.py') --dual-aws --reconnect-only --api-url https://junglemaze.duckdns.org --connect-ip $instance.PublicIpAddress --ssh-alias $SshAlias --output (Join-Path $OutputDirectory 'reconnect')
        if ($LASTEXITCODE -ne 0) { throw 'Independent real-PC WSS rejoin incomplete.' }
        $taskLifecycle.stopStartRejoin='PASS'
        Event 'Independent real-PC WSS rejoin passed; no load repeated.'
    } elseif ($Phase1Connectivity) {
        # No deploy/upload or co-located load: use the already installed backend.
        $env:AWS_ROOM_DEADLINE_UTC = [DateTime]::Parse($taskLifecycle.startRequestedUtc).ToUniversalTime().AddMinutes(20).ToString('o')
        $testArguments=@('-X','utf8',(Join-Path $taskBackend 'Tools/room_benchmark.py'),'--phase1-connectivity','--connect-ip',$instance.PublicIpAddress,'--ssh-alias',$SshAlias,'--output',(Join-Path $OutputDirectory 'test'))
        if ($VpnIp) { $testArguments += @('--vpn-ip',$VpnIp) }
        if ($LocalConfig) { $testArguments += @('--local-config',[IO.Path]::GetFullPath($LocalConfig)) }
        & $Python @testArguments
        if ($LASTEXITCODE -ne 0) { throw 'Phase 1 run failed; inspect test/report.json.' }
        $taskPhase1 = Get-Content -LiteralPath (Join-Path $OutputDirectory 'test/report.json') -Raw | ConvertFrom-Json
        $taskLifecycle.resultStatus = $taskPhase1.status
        if ($taskPhase1.status -eq 'PASS') {
            # Rejoin after a real Stop/Start, within the SAME 20/30-minute budget.
            Event 'Phase 1 connection passed. Stop/Start verification begins.'
            Aws @('ec2','stop-instances','--instance-ids',$InstanceId) | Out-Null
            $restartBy=[DateTime]::UtcNow.AddMinutes(3)
            do {
                if ((State).State.Name -eq 'stopped') { break }
                if ([DateTime]::UtcNow -gt $restartBy) { throw 'Stop/Start: Stop timeout.' }
                Start-Sleep -Seconds 5
            } while ($true)
            if ([DateTime]::UtcNow.AddMinutes(4) -ge [DateTime]::Parse($env:AWS_ROOM_DEADLINE_UTC).ToUniversalTime()) { throw 'Not enough budget for another Start; stop takes priority.' }
            Aws @('ec2','start-instances','--instance-ids',$InstanceId) | Out-Null
            $restartBy=[DateTime]::UtcNow.AddMinutes(3)
            do {
                $instance=State
                $ssm=Aws @('ssm','describe-instance-information','--filters',"Key=InstanceIds,Values=$InstanceId")
                if ($instance.State.Name -eq 'running' -and $ssm.InstanceInformationList.Count -gt 0 -and $ssm.InstanceInformationList[0].PingStatus -eq 'Online') { break }
                if ([DateTime]::UtcNow -gt $restartBy) { throw 'Stop/Start: SSM timeout.' }
                Start-Sleep -Seconds 5
            } while ($true)
            # Transient systemd timer vanished on reboot: re-arm the original absolute deadline.
            $timer=Aws @('ssm','send-command','--instance-ids',$InstanceId,'--document-name','AWS-RunShellScript','--parameters',('file://'+$paramFile))
            $timerOk=$false
            for ($i=0; $i -lt 12; $i++) {
                Start-Sleep -Seconds 2
                try { $inv=Aws @('ssm','get-command-invocation','--command-id',$timer.Command.CommandId,'--instance-id',$InstanceId) } catch { continue }
                if ($inv.Status -eq 'Success') { $timerOk=$true; break }
                if ($inv.Status -in @('Failed','TimedOut','Cancelled')) { break }
            }
            if (-not $timerOk) { throw 'Stop/Start: could not re-arm independent stop timer.' }
            $testArguments=@('-X','utf8',(Join-Path $taskBackend 'Tools/room_benchmark.py'),'--phase1-connectivity','--reconnect-only','--connect-ip',$instance.PublicIpAddress,'--ssh-alias',$SshAlias,'--output',(Join-Path $OutputDirectory 'reconnect'))
            if ($VpnIp) { $testArguments += @('--vpn-ip',$VpnIp) }
            & $Python @testArguments
            if ($LASTEXITCODE -ne 0) { throw 'Stop/Start rejoin incomplete; inspect reconnect/report.json.' }
            $taskReconnect=Get-Content -LiteralPath (Join-Path $OutputDirectory 'reconnect/report.json') -Raw | ConvertFrom-Json
            if ($taskReconnect.status -ne 'PASS') { throw 'Stop/Start UDP rejoin blocked; see reconnect/report.json.' }
            $taskLifecycle.stopStartRejoin='PASS'
            Event 'Real-PC Stop/Start rejoin passed.'
        } else {
            Event ('Phase 1 connectivity '+$taskPhase1.status+'; no stability load or extra Stop/Start attempted.')
        }
    } else {
    Bounded 'ssh' ('-o BatchMode=yes -o ConnectTimeout=15 {0} "df -h /; sudo docker ps --format ''{{{{.Names}}}} {{{{.ID}}}} {{{{.Image}}}}''"' -f $SshAlias) 45
    # Hash-named upload permits safe resumption over slower SSM links; verify before extraction.
    $taskUploadArchive=if ($DeltaRelease) { $DeltaRelease } else { $Release }
    $taskArchiveHash = (Get-FileHash -LiteralPath $taskUploadArchive -Algorithm SHA256).Hash.ToLowerInvariant()
    $taskUploadName = "junglemaze-room-$taskArchiveHash.tar.gz"
    if ($ReuseDeployedRelease) {
        if ($DeltaRelease) {
            # Repacked image IDs differ. Verify full-release provenance AND the
            # actual installed image IDs, rather than comparing packaging hashes.
            $sourceHash=(Get-FileHash -LiteralPath $Release -Algorithm SHA256).Hash.ToLowerInvariant()
            $verifyCode="import json,subprocess`nm=json.load(open('/opt/junglemaze/current/manifest.json'))`nassert m['deltaProvenance']['originalReleaseSha256']=='$sourceHash'`nfor i in m['images']:`n assert json.loads(subprocess.check_output(['docker','image','inspect',i['name']]))[0]['Id']==i['id']`nprint('PASS installed delta provenance and actual image IDs')`n"
            $verifyEncoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($verifyCode))
            Bounded 'ssh' ('-o BatchMode=yes -o ConnectTimeout=15 {0} "echo {1} | base64 -d | sudo python3"' -f $SshAlias,$verifyEncoded) 45
        } else {
        $manifestHash = & $Python -c "import tarfile,hashlib,sys; t=tarfile.open(sys.argv[1]); print(hashlib.sha256(t.extractfile('./SHA256SUMS').read()).hexdigest())" $Release
        if ($LASTEXITCODE -ne 0 -or $manifestHash -notmatch '^[0-9a-f]{64}$') { throw 'Cannot read release manifest checksum.' }
        Bounded 'ssh' ('-o BatchMode=yes -o ConnectTimeout=15 {0} "echo ''{1}  /opt/junglemaze/current/SHA256SUMS'' | sha256sum -c -"' -f $SshAlias,$manifestHash) 30
        }
        Event 'Verified exact installed release; skipping re-upload/re-deploy.'
    } else {
    $batch = Join-Path $OutputDirectory 'upload.sftp'
    # OpenSSH put -a requires an existing remote file; a fresh hash needs normal put.
    # The name is derived only from a validated SHA256, never user shell text.
    $exists=& ssh -o BatchMode=yes -o ConnectTimeout=15 $SshAlias "if test -f ~/$taskUploadName; then echo resume; else echo new; fi"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine upload resume state.' }
    $put=if (($exists | Select-Object -Last 1) -eq 'resume') { 'put -a' } else { 'put' }
    ('{0} "{1}" "{2}"' -f $put,$taskUploadArchive.Replace('\','/'),$taskUploadName) | Set-Content -LiteralPath $batch -Encoding ascii
    Bounded 'sftp' ('-o BatchMode=yes -o ConnectTimeout=15 -B 131072 -R 128 -b "{0}" {1}' -f $batch,$SshAlias) 480
    $taskRestore=if ($DeltaRelease) { 'sudo python3 ~/junglemaze-room-release/delta/restore.py restore ~/junglemaze-room-release && ' } else { '' }
    Bounded 'ssh' ('-o BatchMode=yes -o ConnectTimeout=15 {0} "echo ''{1}  {2}'' | sha256sum -c - && mkdir -p ~/junglemaze-room-release && tar -xzf ~/{2} -C ~/junglemaze-room-release && {3}sudo bash ~/junglemaze-room-release/scripts/deploy.sh ~/junglemaze-room-release"' -f $SshAlias,$taskArchiveHash,$taskUploadName,$taskRestore) 240
    if ($DeltaRelease) { $taskLifecycle.deltaSha256=$taskArchiveHash }
    Event 'App-only release deployed; Mongo/Caddy images and volumes reused.'
    }
    if ($GeneratorOnServer) {
        $clientPack = Join-Path $taskBackend 'out/aws-room-test/smoke-client.tar.gz'
        Bounded 'scp' ('-o BatchMode=yes -o ConnectTimeout=15 "{0}" {1}:junglemaze-test-client.tar.gz' -f $clientPack,$SshAlias) 60
        Bounded 'ssh' ('-o BatchMode=yes -o ConnectTimeout=15 {0} "sudo mkdir -p /opt/junglemaze/test-client && sudo tar -xzf ~/junglemaze-test-client.tar.gz -C /opt/junglemaze/test-client"' -f $SshAlias) 30
    }
    $taskLifecycle.releaseSha256 = (Get-FileHash -LiteralPath $Release -Algorithm SHA256).Hash.ToLowerInvariant()
    Save-Lifecycle
    $env:AWS_ROOM_DEADLINE_UTC = $(if ($DualTransport) { [DateTime]::Parse($taskLifecycle.startRequestedUtc).ToUniversalTime().AddMinutes($BudgetMinutes).ToString('o') } else { $taskDeadline.AddMinutes(-2).ToString('o') })
    $testArguments=@('-X','utf8',(Join-Path $taskBackend 'Tools/room_benchmark.py'),'--aws-session','--api-url','https://junglemaze.duckdns.org','--connect-ip',$instance.PublicIpAddress,'--ssh-alias',$SshAlias,'--output',(Join-Path $OutputDirectory 'test'))
    if ($GeneratorOnServer) { $testArguments += '--generator-on-server' }
    if ($CapacityAfter) { $testArguments += @('--capacity-after',$CapacityAfter) }
    if ($DualTransport) {
        $taskLocalImage=& $Python -c "import tarfile,json,sys; t=tarfile.open(sys.argv[1]); m=json.load(t.extractfile('./manifest.json')); print(next(i['name'] for i in m['images'] if 'junglemaze-game:' in i['name']))" $Release
        if ($LASTEXITCODE -ne 0 -or $taskLocalImage -notmatch '^junglemaze-game:[a-z0-9-]+$') { throw 'Local GameServer image not found in release manifest.' }
        $testArguments += @('--dual-aws','--local-image',$taskLocalImage)
    }
    & $Python @testArguments
    if ($LASTEXITCODE -ne 0) { throw 'AWS test incomplete; inspect test/report.json.' }
    if ($DualTransport) {
        Event 'WSS/Auto and concurrent Local completed; checking Stop/Start.'
        $taskRejoinIp=Restart-Guarded
        Probe-PublicHttps $taskRejoinIp
        $rejoinArgs=@('-X','utf8',(Join-Path $taskBackend 'Tools/room_benchmark.py'),'--dual-aws','--reconnect-only','--api-url','https://junglemaze.duckdns.org','--connect-ip',$taskRejoinIp,'--ssh-alias',$SshAlias,'--output',(Join-Path $OutputDirectory 'reconnect'))
        & $Python @rejoinArgs
        if ($LASTEXITCODE -ne 0) { throw 'WSS rejoin after restart incomplete.' }
        $taskLifecycle.stopStartRejoin='PASS'
    }
    Event 'Room protocol and load tests completed.'
    }
} catch {
    $taskLifecycle.error = $_.Exception.Message
    Event ('Aborted: ' + $_.Exception.Message)
} finally {
    if ($taskLifecycle.startedByThisRun -or $taskLifecycle.adoptedRunningInstance) {
        # Stop remains mandatory even when deployment/JWT/room cleanup fails.
        $taskLifecycle.stopRequestedUtc = [DateTime]::UtcNow.ToString('o')
        for ($attempt=0; $attempt -lt 3; $attempt++) {
            try { Aws @('ec2','stop-instances','--instance-ids',$InstanceId) | Out-Null; break } catch { Event "Stop attempt $attempt failed; watchdog remains armed." }
        }
        $stopBy = [DateTime]::UtcNow.AddMinutes(3)
        do {
            try { if ((State).State.Name -eq 'stopped') { $taskLifecycle.stoppedConfirmed=$true; break } } catch { }
            Start-Sleep -Seconds 5
        } while ([DateTime]::UtcNow -lt $stopBy)
        if ($taskLifecycle.stoppedConfirmed) {
            Event 'Confirmed EC2 Stopped. Compute billing ended; EBS storage remains.'
            if ($taskWatchdog -and -not $taskWatchdog.HasExited) { Stop-Process -Id $taskWatchdog.Id }
        } else { Event 'STOP NOT CONFIRMED. Watchdog/timer remain armed; verify AWS Console immediately.' }
    }
    Remove-Item Env:AWS_ROOM_DEADLINE_UTC,Env:AWS_ROOM_FIXTURES -ErrorAction SilentlyContinue
    if ($taskSleepHeld) { [JungleMaze.TestPower]::Release() }
    Save-Lifecycle
}
if ($taskLifecycle.error -or (($taskLifecycle.startedByThisRun -or $taskLifecycle.adoptedRunningInstance) -and -not $taskLifecycle.stoppedConfirmed)) { exit 1 }
