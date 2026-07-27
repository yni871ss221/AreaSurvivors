param(
    [Parameter(Mandatory = $true)][string]$Pattern,
    [string[]]$Path = @("Assets/AreaSurvivors"),
    [Alias("First")]
    [int]$TopFiles = 3,
    [int]$Context = 12,
    [int]$MaxMatchesPerFile = 2,
    [Alias("Include")]
    [string[]]$Extension = @("cs"),
    [string]$GraphifyFallbackId = "",
    [ValidateSet("production", "evaluation")]
    [string]$GraphifyUsageCategory = "production",
    [switch]$IncludeUnityYaml,
    [switch]$PrintOutput
)

$ErrorActionPreference = "Stop"
$focusedSearchStartedAt = Get-Date

foreach ($item in $Path) {
    if ([string]::IsNullOrWhiteSpace($item) -or -not (Test-Path -LiteralPath $item)) {
        throw "Each -Path item must exist. For powershell -File calls, use one path per invocation instead of a comma-joined value: $item"
    }
}

if ($PrintOutput) {
    $linesPerMatch = ($Context * 2) + 4
    $maxInteractiveMatches = [Math]::Max(1, [Math]::Floor(80 / $linesPerMatch))
    if ($MaxMatchesPerFile -gt $maxInteractiveMatches) {
        Write-Warning "focused-search caps -MaxMatchesPerFile at $maxInteractiveMatches for -Context $Context with -PrintOutput so each delegated safe-read remains within 80 lines."
        $MaxMatchesPerFile = $maxInteractiveMatches
    }
}

function Quote-PowerShellValue {
    param([Parameter(Mandatory = $true)][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

$extraArgs = @()
foreach ($ext in $Extension) {
    $trimmed = $ext.Trim().TrimStart("*").TrimStart(".")
    if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
        $extraArgs += "-g"
        $extraArgs += Quote-PowerShellValue "*.$trimmed"
    }
}
if (-not $IncludeUnityYaml) {
    $extraArgs += "-g '!*.unity'"
    $extraArgs += "-g '!*.prefab'"
    $extraArgs += "-g '!*.asset'"
}

$pathArgs = ($Path | ForEach-Object { Quote-PowerShellValue $_ }) -join " "
$patternArg = Quote-PowerShellValue $Pattern
$extra = $extraArgs -join " "
$command = "`$items = @(rg -l --hidden -g '!Library/**' -g '!Temp/**' -g '!Obj/**' -g '!.git/**' $extra $patternArg $pathArgs 2>&1); `$rgExit = `$LASTEXITCODE; if (`$rgExit -gt 1) { `$items | ForEach-Object { [Console]::Error.WriteLine(`$_) }; exit `$rgExit }; `$items | Select-Object -First $TopFiles; exit 0"
$json = & "$PSScriptRoot\Safe-Command.ps1" -Command $command -Json
$record = $json | ConvertFrom-Json
if ($record.exit_code -ne 0) {
    throw "focused-search file discovery failed with exit code $($record.exit_code). Capture: $($record.capture_path)"
}
$filesText = if (Test-Path -LiteralPath $record.capture_path) { Get-Content -LiteralPath $record.capture_path -Encoding UTF8 } else { @() }
$files = @($filesText | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

Write-Output "Focused files:"
foreach ($file in $files) { Write-Output ("- {0}" -f $file) }

foreach ($file in $files) {
    Write-Output ""
    Write-Output ("[focused-search] {0}" -f $file)
    & "$PSScriptRoot\safe-read.ps1" -Path $file -Pattern $Pattern -Context $Context -MaxMatches $MaxMatchesPerFile -PrintOutput:$PrintOutput
}

if (-not [string]::IsNullOrWhiteSpace($GraphifyFallbackId)) {
    $projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $usageLogPath = Join-Path $projectRoot "TokenReports\graphify-pilot-usage.jsonl"
    $fallbackRecord = [ordered]@{
        timestamp = (Get-Date).ToString("o")
        graphify_version = "0.9.26"
        usage_category = $GraphifyUsageCategory
        action = "Fallback"
        source = $Pattern
        target = ($Path -join ";")
        elapsed_ms = [int]((Get-Date) - $focusedSearchStartedAt).TotalMilliseconds
        exit_code = 0
        fallback_id = $GraphifyFallbackId
        fallback_executed = $true
    }
    $fallbackJsonLine = $fallbackRecord | ConvertTo-Json -Compress -Depth 3
    [System.IO.File]::AppendAllText(
        $usageLogPath,
        $fallbackJsonLine + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false)
    )
    Write-Output ("graphify_fallback_recorded: {0}" -f $GraphifyFallbackId)
}
