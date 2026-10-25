param([ValidateSet('public','vpn')][string]$Mode='vpn',[string]$Domain='',[string]$AcmeEmail='',[string]$SwaggerUser='hostadmin',[Security.SecureString]$SwaggerPassword,[string]$OutputFile='')
$ErrorActionPreference='Stop'
if ($Mode -eq 'public' -and ($Domain -notmatch '^[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$' -or $Domain -eq 'junglemaze.duckdns.org' -or $AcmeEmail -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$')) { throw 'Provide a selfhost domain distinct from AWS and a valid ACME email.' }
if ($SwaggerUser -notmatch '^[a-zA-Z0-9_-]{3,32}$') { throw 'Invalid Swagger username' }
$path=if ($OutputFile) {[IO.Path]::GetFullPath($OutputFile)} else {Join-Path $PSScriptRoot ('.env.host.'+$Mode)}
if (Test-Path -LiteralPath $path) { throw 'Private config already exists; kept unchanged. Edit it manually instead of regenerating secrets.' }
function Random-Hex([int]$Count) { $b=New-Object byte[] $Count;$rng=[Security.Cryptography.RandomNumberGenerator]::Create();try{$rng.GetBytes($b)}finally{$rng.Dispose()};return [BitConverter]::ToString($b).Replace('-','').ToLowerInvariant() }
$secure=if ($SwaggerPassword) {$SwaggerPassword} else {Read-Host 'Mat khau bao ve Swagger (>=12 ky tu)' -AsSecureString}
$pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $plain=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ($plain.Length -lt 12) { throw 'Swagger password must have at least12 characters' }
    $info=[Diagnostics.ProcessStartInfo]::new();$info.FileName='docker';$info.Arguments='run --rm --network none -i caddy:2 caddy hash-password --algorithm bcrypt'
    $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardInput=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $p=[Diagnostics.Process]::Start($info);$stdout=$p.StandardOutput.ReadToEndAsync();$stderr=$p.StandardError.ReadToEndAsync()
    $p.StandardInput.WriteLine($plain);$p.StandardInput.Close();$plain=$null
    if (-not $p.WaitForExit(30000)) { $p.Kill();throw 'Caddy hash deadline' }
    $hash=$stdout.GetAwaiter().GetResult().Trim()
    if ($p.ExitCode -ne 0 -or $hash -notmatch '^\$2[aby]\$\d\d\$.{53}$') { throw 'Caddy password hash failed; check Docker Desktop and caddy:2 image' }
} finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer);$plain=$null;$secure.Dispose() }
$public=($Mode -eq 'public').ToString().ToLowerInvariant()
$localHttp=($Mode -eq 'vpn').ToString().ToLowerInvariant()
$lines=@(
    ('COMPOSE_PROFILES='+$Mode), ('HOST_DOMAIN='+$Domain), ('ACME_EMAIL='+$AcmeEmail), ('SWAGGER_USER='+$SwaggerUser),
    ("SWAGGER_HASH='"+$hash+"'"), ('PUBLIC_HOSTING='+$public), ('ALLOW_LOCAL_HTTP='+$localHttp),
    'MATCH_PROFILE=main','AUTH_MODE=RoomCode',('ROOM_CODE='+((Random-Hex 6).Substring(0,6).ToUpperInvariant())),
    ('JWT_SIGNING_KEY='+(Random-Hex 32)),('ROOM_CONTROL_TOKEN='+(Random-Hex 32)),('MATCH_RESULT_TOKEN='+(Random-Hex 32)),
    'MAX_PLAYERS=10','MAX_ROOM_PLAYERS=10','MAX_TOTAL_PLAYERS=50','MAX_ROOMS=5','MAX_ROOMS_PER_USER=2',
    'HOST_GAME_UDP_BIND=0.0.0.0','HOST_GAME_UDP_PORT=7777','HOST_VPN_PORT=8080'
)
[IO.File]::WriteAllText($path,($lines -join "`n")+"`n",[Text.UTF8Encoding]::new($false))
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User
$acl=Get-Acl -LiteralPath $path;$acl.SetAccessRuleProtection($true,$false);$acl.SetOwner($sid)
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','Allow'));Set-Acl -LiteralPath $path -AclObject $acl
Write-Output "Private config created: $path. No services/firewall/router settings were changed. Read only ROOM_CODE when sharing the join code."
