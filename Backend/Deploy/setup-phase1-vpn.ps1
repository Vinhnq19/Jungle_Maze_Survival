param(
    [ValidateSet('PC','EC2')][string]$Target = 'PC',
    [string]$SshAlias = 'junglemaze'
)
$ErrorActionPreference='Stop'
$taskTailscale='C:/Program Files/Tailscale/tailscale.exe'
if ($Target -eq 'PC') {
    # Fail before changing the PC if this network cannot reach the installer/login.
    foreach ($taskUrl in @('https://pkgs.tailscale.com/stable/','https://login.tailscale.com/')) {
        try { Invoke-WebRequest -UseBasicParsing -Uri $taskUrl -TimeoutSec 15 | Out-Null }
        catch { throw "Network/DNS blocks $taskUrl. Use an allowed network/hotspot or ask its administrator; EC2 has not been started by this script." }
    }
    if (-not (Test-Path -LiteralPath $taskTailscale)) {
        & winget install --id Tailscale.Tailscale --exact --silent --accept-package-agreements --accept-source-agreements
        if ($LASTEXITCODE -ne 0) { throw 'Tailscale install failed. Complete the Windows installer/UAC before continuing.' }
    }
    # No auth keys in argv or files. User signs in with their own browser identity.
    & $taskTailscale up --accept-dns=false --accept-routes=false --timeout=10s
    if ($LASTEXITCODE -ne 0) {
        Write-Output 'Open the login URL shown above, authenticate, then run this command again.'
        exit 1
    }
    & $taskTailscale status
} else {
    if (-not (Test-Path -LiteralPath $taskTailscale)) { throw 'Set up and sign in the PC first, while EC2 is stopped.' }
    $taskState=& $taskTailscale status --json | ConvertFrom-Json
    if ($taskState.BackendState -ne 'Running') { throw 'Sign in the PC first. This script does not start EC2.' }
    # This path uses the existing SSM SSH alias and never starts/resizes an instance.
    $taskRemote=@'
set -Eeuo pipefail
if ! command -v tailscale >/dev/null; then
  temporary=$(mktemp)
  trap 'rm -f -- "$temporary"' EXIT
  curl --fail --silent --show-error --location --max-time 30 https://tailscale.com/install.sh -o "$temporary"
  timeout 120 bash "$temporary"
fi
tailscale up --accept-dns=false --accept-routes=false --hostname=junglemaze-ec2 --timeout=10s
tailscale ip -4
'@
    # Avoid PowerShell's CRLF pipeline changing Bash arguments on Linux.
    $taskPayload=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($taskRemote.Replace("`r",'')))
    & ssh -o BatchMode=yes -o ConnectTimeout=30 $SshAlias "printf '%s' '$taskPayload' | base64 -d | sudo bash"
    if ($LASTEXITCODE -ne 0) { throw 'EC2 VPN enrollment incomplete. Authenticate its login URL with the same tailnet, rerun, and Stop EC2 if finished.' }
}
