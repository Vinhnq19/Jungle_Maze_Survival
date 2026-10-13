param(
    [string]$Domain = 'junglemaze.duckdns.org',
    [string]$OutputDirectory = '',
    [string]$ReleaseId = ('aws-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$taskBackend = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $taskBackend 'out/aws' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ($Domain -notmatch '^[a-z0-9-]+\.duckdns\.org$' -or $ReleaseId -notmatch '^[a-z0-9-]+$') { throw 'Invalid hostname/release ID.' }
function Invoke-Docker([string[]]$DockerArguments) {
    & docker @DockerArguments
    if ($LASTEXITCODE -ne 0) { throw "Docker operation failed: $($DockerArguments[0])" }
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskStage = Join-Path $OutputDirectory ('stage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskStage | Out-Null
$taskApiImage = "junglemaze-api:$ReleaseId"
$taskGameImage = "junglemaze-game:$ReleaseId"
$taskMongoImage = "junglemaze-mongo:$ReleaseId"
$taskCaddyImage = "junglemaze-caddy:$ReleaseId"
try {
    if (-not $SkipBuild) {
        Invoke-Docker @('build', '--platform', 'linux/amd64', '-t', $taskApiImage, '-f', (Join-Path $taskBackend 'Api/Dockerfile'), $taskBackend)
        Invoke-Docker @('build', '--platform', 'linux/amd64', '-t', $taskGameImage, '-f', (Join-Path $taskBackend 'GameServer/Dockerfile'), $taskBackend)
        # MongoDB 7 không dùng allocator bị lỗi với kernel 6.19/7.0 của EC2.
        # Khóa patch cụ thể; deploy kiểm tra volume trước khi chuyển từ release dùng MongoDB 8.
        Invoke-Docker @('pull', '--platform', 'linux/amd64', 'mongo:7.0.43')
        Invoke-Docker @('pull', '--platform', 'linux/amd64', 'caddy:2')
        Invoke-Docker @('tag', 'mongo:7.0.43', $taskMongoImage)
        Invoke-Docker @('tag', 'caddy:2', $taskCaddyImage)
    }
    $taskImageNames = @($taskApiImage, $taskGameImage, $taskMongoImage, $taskCaddyImage)
    # Nhận hết stdout rồi mới lấy version; Select-Object -First ngay trên process gây SIGPIPE ở PowerShell 5.
    $taskMongoInfo = & docker run --rm --network none --entrypoint mongod $taskMongoImage --version
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read MongoDB version.' }
    $taskMongoVersion = ($taskMongoInfo | Select-Object -First 1).Trim()
    if ($taskMongoVersion -notmatch '^db version v7\.0\.43$') { throw 'AWS release requires MongoDB 7.0.43; retag the correct Mongo image.' }
    $taskImages = foreach ($taskName in $taskImageNames) {
        $taskInspect = (& docker image inspect $taskName | ConvertFrom-Json)[0]
        if ($LASTEXITCODE -ne 0 -or $taskInspect.Os -ne 'linux' -or $taskInspect.Architecture -ne 'amd64') { throw 'Every release image must be Linux amd64.' }
        [ordered]@{ name = $taskName; id = $taskInspect.Id; os = $taskInspect.Os; architecture = $taskInspect.Architecture }
    }
    # Allowlist: chỉ chép cấu hình không secret, script và docs. Không chép Backend/.env hoặc source cache.
    Copy-Item -LiteralPath (Join-Path $taskBackend 'compose.aws.yaml') -Destination $taskStage
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Aws/Caddyfile') -Destination $taskStage
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Aws/scripts') -Destination $taskStage -Recurse
    New-Item -ItemType Directory -Path (Join-Path $taskStage 'docs') | Out-Null
    # Kèm tài liệu assets vì hai hướng dẫn chính có liên kết tới bản đồ module JSurviv.
    foreach ($taskDocName in @('Phase1_First4Tasks.vi.md', 'Network_Architecture.vi.md', 'AWS_SSH_Swagger_Compass.vi.md', 'JSurviv_Integration.vi.md', 'JSurviv_Verification.vi.md')) {
        Copy-Item -LiteralPath (Join-Path $taskBackend "../Docs/$taskDocName") -Destination (Join-Path $taskStage 'docs')
    }
    $taskClient = [ordered]@{ mode='Online'; apiBaseUrl="https://$Domain"; gameServerHost=$Domain; gameServerPort=7777; authMode='RoomCode'; protocolVersion=1; connectTimeoutMs=5000 }
    $taskClient | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskStage 'client.aws.json') -Encoding utf8
    @("RELEASE_ID=$ReleaseId", "DEFAULT_DOMAIN=$Domain", "API_IMAGE=$taskApiImage", "GAME_IMAGE=$taskGameImage", "MONGO_IMAGE=$taskMongoImage", 'MONGO_MAJOR=7', "CADDY_IMAGE=$taskCaddyImage") | Set-Content -LiteralPath (Join-Path $taskStage 'release.env') -Encoding utf8
    [ordered]@{ releaseId=$ReleaseId; builtAtUtc=[DateTime]::UtcNow.ToString('o'); domain=$Domain; region='ap-southeast-2'; images=@($taskImages); mongoVersion='7.0.43'; databaseMigration='new/empty AWS database only when switching from MongoDB 8'; publicGameAuth='RoomCode' } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskStage 'manifest.json') -Encoding utf8
    # Chuẩn hóa LF/UTF-8 không BOM để Bash Linux chạy được dù editor trên Windows dùng CRLF.
    foreach ($taskTextFile in Get-ChildItem -LiteralPath $taskStage -File -Recurse) {
        $taskText = [IO.File]::ReadAllText($taskTextFile.FullName).Replace("`r`n", "`n")
        [IO.File]::WriteAllText($taskTextFile.FullName, $taskText, [Text.UTF8Encoding]::new($false))
    }
    $taskTar = Join-Path $taskStage 'images.tar'
    Invoke-Docker (@('save', '-o', $taskTar) + $taskImageNames)
    $taskInput = [IO.File]::OpenRead($taskTar)
    $taskOutput = [IO.File]::Create((Join-Path $taskStage 'images.tar.gz'))
    $taskGzip = [IO.Compression.GZipStream]::new($taskOutput, [IO.Compression.CompressionLevel]::Optimal)
    try { $taskInput.CopyTo($taskGzip) } finally { $taskGzip.Dispose(); $taskOutput.Dispose(); $taskInput.Dispose() }
    Remove-Item -LiteralPath $taskTar
    $taskHashLines = foreach ($taskFile in Get-ChildItem -LiteralPath $taskStage -File -Recurse | Sort-Object FullName) {
        $taskRelative = $taskFile.FullName.Substring($taskStage.Length + 1).Replace('\', '/')
        (Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $taskRelative
    }
    [IO.File]::WriteAllText((Join-Path $taskStage 'SHA256SUMS'), ($taskHashLines -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
    $taskArchive = Join-Path $OutputDirectory 'release.tar.gz'
    & tar -czf $taskArchive -C $taskStage .
    if ($LASTEXITCODE -ne 0) { throw 'Cannot create release.tar.gz.' }
    $taskArchiveHash = (Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText(($taskArchive + '.sha256'), "$taskArchiveHash  release.tar.gz`n", [Text.UTF8Encoding]::new($false))
    Write-Output "Release: $taskArchive"
    Write-Output "Release ID: $ReleaseId"
    Write-Output "SHA256: $taskArchiveHash"
    Write-Output ('Size MB: ' + [Math]::Round((Get-Item -LiteralPath $taskArchive).Length / 1MB, 1))
} finally {
    # Chỉ xóa stage GUID do script tự tạo, đã kiểm tra absolute path ở trong output directory.
    $taskResolvedStage = [IO.Path]::GetFullPath($taskStage)
    $taskOutputPrefix = $OutputDirectory.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ($taskResolvedStage.StartsWith($taskOutputPrefix, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $taskResolvedStage -Leaf) -match '^stage-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $taskResolvedStage -Recurse -Force
    }
}
