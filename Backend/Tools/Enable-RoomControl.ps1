param([string]$EnvFile = (Join-Path $PSScriptRoot '../.env'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $EnvFile)) { throw 'Create Backend/.env from .env.example first.' }
$content = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $EnvFile))
$entries = [regex]::Matches($content, '(?m)^ROOM_CONTROL_TOKEN=(.*)$')
if ($entries.Count -gt 1) { throw 'Duplicate ROOM_CONTROL_TOKEN. Edit .env manually; secret was not printed.' }
if ($entries.Count -eq 1 -and $entries[0].Groups[1].Value.Trim().Trim("'", '"').Length -gt 0) {
    Write-Host 'Existing room control key preserved.'
    return
}
# A distinct 32-byte secret; never reuse a JWT key or join code.
$bytes = New-Object byte[] 32
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$key = -join ($bytes | ForEach-Object { $_.ToString('x2') })
$content = [regex]::Replace($content, '(?m)^ROOM_CONTROL_TOKEN=.*\r?\n?', '')
[IO.File]::WriteAllText((Resolve-Path -LiteralPath $EnvFile), $content.TrimEnd() + "`nROOM_CONTROL_TOKEN=$key`n", (New-Object Text.UTF8Encoding $false))
Write-Host 'Private room control key added to .env. Recreate API and GameServer to apply it.'
