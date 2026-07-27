param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [string]$CompanyName = "Codex",
    [string]$ProductName = "Area Survivors"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProjectRoot -PathType Container)) {
    throw "ProjectRoot must be an existing directory: $ProjectRoot"
}

$resolvedProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$buildRoot = Join-Path $resolvedProjectRoot "Build"
$executablePath = Join-Path $buildRoot "$ProductName.exe"
$dataPath = Join-Path $buildRoot "${ProductName}_Data"
$steamAppIdPath = Join-Path $buildRoot "steam_appid.txt"
$localAppData = [Environment]::GetFolderPath("LocalApplicationData")
$localLowRoot = Join-Path (Split-Path -Parent $localAppData) "LocalLow"
$playerLogPath = Join-Path (Join-Path (Join-Path $localLowRoot $CompanyName) $ProductName) "Player.log"

$playerLogLines = @()
if (Test-Path -LiteralPath $playerLogPath -PathType Leaf) {
    $playerLogLines = @(Get-Content -LiteralPath $playerLogPath -Encoding UTF8)
}

$startupSignals = @($playerLogLines |
    Where-Object { $_ -match "Initialize engine version|UnloadTime:|ShutdownInProgress|Exception|Error|Crash|Abort|Steam|Scene" } |
    Select-Object -First 20 |
    ForEach-Object { $_.ToString() })

$report = [ordered]@{
    project_root = $resolvedProjectRoot
    build_root = $buildRoot
    executable = [ordered]@{
        path = $executablePath
        exists = Test-Path -LiteralPath $executablePath -PathType Leaf
        last_write_time = if (Test-Path -LiteralPath $executablePath -PathType Leaf) { (Get-Item -LiteralPath $executablePath).LastWriteTime.ToString("o") } else { $null }
    }
    data_directory = [ordered]@{
        path = $dataPath
        exists = Test-Path -LiteralPath $dataPath -PathType Container
        last_write_time = if (Test-Path -LiteralPath $dataPath -PathType Container) { (Get-Item -LiteralPath $dataPath).LastWriteTime.ToString("o") } else { $null }
    }
    steam_appid = [ordered]@{
        path = $steamAppIdPath
        exists = Test-Path -LiteralPath $steamAppIdPath -PathType Leaf
        value = if (Test-Path -LiteralPath $steamAppIdPath -PathType Leaf) { (Get-Content -LiteralPath $steamAppIdPath -Encoding UTF8 -Raw).Trim() } else { $null }
    }
    player_log = [ordered]@{
        path = $playerLogPath
        exists = Test-Path -LiteralPath $playerLogPath -PathType Leaf
        last_write_time = if (Test-Path -LiteralPath $playerLogPath -PathType Leaf) { (Get-Item -LiteralPath $playerLogPath).LastWriteTime.ToString("o") } else { $null }
        line_count = $playerLogLines.Count
        startup_signals = $startupSignals
    }
}

$report | ConvertTo-Json -Depth 6
