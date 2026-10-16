param(
    [Parameter(Mandatory=$true)][ValidateRange(5,240)][int]$Minutes,
    [string]$AwsCli='D:/AWS/ConnectionSetup/AWSCLI-portable/aws.exe',
    [string]$Profile='junglemaze-admin',
    [string]$Region='ap-southeast-2',
    [string]$InstanceId='i-0e617f8072fbb9878'
)
$ErrorActionPreference='Stop'
$taskOwned=$false
function Aws([string[]]$Arguments) {
    $raw=& $AwsCli @Arguments --profile $Profile --region $Region --output json --no-cli-pager --cli-connect-timeout 5 --cli-read-timeout 15
    if($LASTEXITCODE -ne 0){throw 'AWS command failed; check CLI login/permissions.'}
    if($raw){return ($raw -join '')|ConvertFrom-Json}
}
function State {(Aws @('ec2','describe-instances','--instance-ids',$InstanceId)).Reservations[0].Instances[0]}
try {
    if((State).State.Name -ne 'stopped'){throw 'Require Stopped; do not replace another active session or its deadline.'}
    $shutdown=Aws @('ec2','describe-instance-attribute','--instance-id',$InstanceId,'--attribute','instanceInitiatedShutdownBehavior')
    if($shutdown.InstanceInitiatedShutdownBehavior.Value -ne 'stop'){throw 'Shutdown must Stop, not Terminate.'}
    $credit=Aws @('ec2','describe-instance-credit-specifications','--instance-ids',$InstanceId)
    if($credit.InstanceCreditSpecifications[0].CpuCredits -ne 'standard'){throw 'CPU credits must be Standard.'}
    $taskDeadline=[DateTime]::UtcNow.AddMinutes($Minutes)
    # Independent PC guard remains useful if SSM cannot be reached after Start.
    $guard=@(
        ('$deadline=[DateTime]::Parse(''{0}'').ToUniversalTime()' -f $taskDeadline.ToString('o')),
        'while([DateTime]::UtcNow -lt $deadline){Start-Sleep -Seconds 5}',
        ('& ''{0}'' ec2 stop-instances --instance-ids ''{1}'' --profile ''{2}'' --region ''{3}'' --no-cli-pager --cli-connect-timeout 5 --cli-read-timeout 15 | Out-Null' -f $AwsCli,$InstanceId,$Profile,$Region)
    ) -join [Environment]::NewLine
    $encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($guard))
    Start-Process powershell -WindowStyle Hidden -ArgumentList "-NoProfile -EncodedCommand $encoded" | Out-Null
    $taskOwned=$true
    Aws @('ec2','start-instances','--instance-ids',$InstanceId)|Out-Null
    $readyBy=[DateTime]::UtcNow.AddMinutes(3)
    do {
        $instance=State
        $ssm=Aws @('ssm','describe-instance-information','--filters',"Key=InstanceIds,Values=$InstanceId")
        if($instance.State.Name -eq 'running' -and $ssm.InstanceInformationList.Count -gt 0 -and $ssm.InstanceInformationList[0].PingStatus -eq 'Online'){break}
        if([DateTime]::UtcNow -ge $readyBy){throw 'SSM unavailable; abort scheduled session.'}
        Start-Sleep -Seconds 5
    } while($true)
    # The EC2 timer survives losing the PC/SSH connection. It is armed before handing out the endpoint.
    $stamp=$taskDeadline.ToString('yyyy-MM-dd HH:mm:ss')+' UTC'
    $parameters=@{commands=@("systemd-run --quiet --unit=junglemaze-play-stop --on-calendar='$stamp' /usr/sbin/shutdown -h now","systemctl restart junglemaze-dns.service")}|ConvertTo-Json -Compress
    $parameterFile=Join-Path ([IO.Path]::GetTempPath()) ('junglemaze-play-'+[Guid]::NewGuid().ToString('N')+'.json')
    [IO.File]::WriteAllText($parameterFile,$parameters,[Text.UTF8Encoding]::new($false))
    $command=Aws @('ssm','send-command','--instance-ids',$InstanceId,'--document-name','AWS-RunShellScript','--parameters',('file://'+$parameterFile))
    $ok=$false
    for($i=0;$i -lt 20;$i++) {
        Start-Sleep -Seconds 2
        try{$inv=Aws @('ssm','get-command-invocation','--command-id',$command.Command.CommandId,'--instance-id',$InstanceId)}catch{continue}
        if($inv.Status -eq 'Success'){$ok=$true;break}
        if($inv.Status -in @('Failed','TimedOut','Cancelled')){break}
    }
    if(-not $ok){throw 'Server Stop timer/DNS update not confirmed.'}
    Write-Output ('Server booted. Scheduled Stop UTC+7: '+([DateTimeOffset]$taskDeadline).ToOffset([TimeSpan]::FromHours(7)).ToString('yyyy-MM-dd HH:mm:ss'))
    Write-Output ('Current IPv4: '+$instance.PublicIpAddress)
    Write-Output 'If client DNS is stale: explicitly set NET_GAME_SERVER_HOST and NET_WS_CONNECT_IP to this IPv4; WSS keeps hostname/certificate validation. This does not fix browser DNS.'
    Write-Output 'Verify HTTPS /health and one Auto client before inviting players.'
    Write-Output 'API: https://junglemaze.duckdns.org/swagger | WSS: wss://junglemaze.duckdns.org/game'
    Write-Output 'Stop early via AWS Console when finished. EBS remains billable after Stop.'
} catch {
    $taskFailure=$_
    if($taskOwned){
        for($attempt=0;$attempt -lt 3;$attempt++){
            try{Aws @('ec2','stop-instances','--instance-ids',$InstanceId)|Out-Null;break}catch{}
        }
        $stopBy=[DateTime]::UtcNow.AddMinutes(3)
        $stopped=$false
        do{
            try{if((State).State.Name -eq 'stopped'){$stopped=$true;break}}catch{}
            Start-Sleep -Seconds 5
        }while([DateTime]::UtcNow -lt $stopBy)
        if($stopped){Write-Output 'Failure cleanup: confirmed EC2 Stopped.'}
        else{Write-Warning 'STOP NOT CONFIRMED. Watchdog remains armed; check AWS Console immediately.'}
    }
    throw $taskFailure
} finally {
    if($parameterFile -and (Test-Path -LiteralPath $parameterFile)){Remove-Item -LiteralPath $parameterFile}
}
