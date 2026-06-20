param(
    [switch]$IncludeUnity,
    [string]$ReportPath,
    [string]$BaselinePath = "",
    [switch]$UpdateBaseline,
    [int]$WarnIncreasePercent = 10,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "TokenUsageCommon.ps1")

$commands = @(
    "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-status.ps1",
    "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/guarded-command.ps1 -Command 'git diff'",
    "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-search.ps1 -Pattern 'BuildMode' -Path Assets/AreaSurvivors/Scripts",
    "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-read.ps1 -Path AGENTS.md -First 80",
    "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-report-summary.ps1 -Days 1 -Top 5 -ExcludeBenchmark"
)

if ($IncludeUnity) {
    $commands += @(
        "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-unity.ps1 -Action Compile",
        "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-unity.ps1 -Action ConsoleErrors -MaxCount 30",
        "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-unity-search.ps1 -Query BuildMode"
    )
}

$results = @()
foreach ($command in $commands) {
    $capturePath = Join-Path ([System.IO.Path]::GetTempPath()) ("token-daily-" + [guid]::NewGuid().ToString("N") + ".txt")
    $escapedPath = $capturePath.Replace("'", "''")
    $script = "& { $command } *>&1 | Out-File -LiteralPath '$escapedPath' -Encoding utf8; if (`$global:LASTEXITCODE -ne `$null) { exit `$global:LASTEXITCODE }"
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command $script | Out-Null
    $exitCode = $LASTEXITCODE
    $text = if (Test-Path -LiteralPath $capturePath) { [System.IO.File]::ReadAllText($capturePath) } else { "" }
    $estimate = Get-TokenUsageEstimate -Text $text -Source $command
    $record = [pscustomobject]@{
        timestamp = (Get-Date).ToString("o")
        kind = "daily_health"
        command = $command
        exit_code = $exitCode
        capture_path = $capturePath
        estimate = $estimate
        advice = Get-TokenUsageAdvice -Estimate $estimate
    }
    Write-TokenUsageJsonLine -Record $record -ReportPath $ReportPath | Out-Null
    $results += $record
}

if ([string]::IsNullOrWhiteSpace($BaselinePath)) {
    $BaselinePath = Join-Path (Get-Location) "TokenReports\token-daily-baseline.json"
}

$comparison = @()
if (Test-Path -LiteralPath $BaselinePath) {
    $baseline = Get-Content -LiteralPath $BaselinePath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($result in $results) {
        $previous = @($baseline.results | Where-Object { $_.command -eq $result.command } | Select-Object -First 1)
        if ($previous.Count -eq 0) { continue }
        $oldTokens = [int]$previous.estimated_tokens
        $newTokens = [int]$result.estimate.estimated_tokens
        $delta = $newTokens - $oldTokens
        $deltaPercent = if ($oldTokens -eq 0) { 0 } else { [math]::Round(($delta / $oldTokens) * 100, 1) }
        $comparison += [pscustomobject]@{
            command = $result.command
            baseline_tokens = $oldTokens
            current_tokens = $newTokens
            delta_tokens = $delta
            delta_percent = $deltaPercent
            status = if ($deltaPercent -ge $WarnIncreasePercent) { "increased" } elseif ($delta -lt 0) { "improved" } else { "same" }
        }
    }
}

if ($UpdateBaseline -or -not (Test-Path -LiteralPath $BaselinePath)) {
    $baselineRoot = Split-Path -Parent $BaselinePath
    if (-not [string]::IsNullOrWhiteSpace($baselineRoot) -and -not (Test-Path -LiteralPath $baselineRoot)) {
        New-Item -ItemType Directory -Force -Path $baselineRoot | Out-Null
    }
    [pscustomobject]@{
        timestamp = (Get-Date).ToString("o")
        results = @($results | ForEach-Object {
            [pscustomobject]@{
                command = $_.command
                estimated_tokens = $_.estimate.estimated_tokens
                risk = $_.estimate.risk
                lines = $_.estimate.lines
                chars = $_.estimate.chars
            }
        })
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $BaselinePath -Encoding UTF8
}

if ($Json) {
    [pscustomobject]@{
        results = $results
        comparison = $comparison
        baseline_path = $BaselinePath
    } | ConvertTo-Json -Depth 8
} else {
    $results |
        Select-Object @{Name="tokens";Expression={$_.estimate.estimated_tokens}},
                      @{Name="risk";Expression={$_.estimate.risk}},
                      @{Name="exit";Expression={$_.exit_code}},
                      command |
        Format-Table -AutoSize

    if ($comparison.Count -gt 0) {
        Write-Output ""
        Write-Output "Comparison to daily baseline:"
        $comparison |
            Select-Object status, delta_percent, delta_tokens, baseline_tokens, current_tokens, command |
            Format-Table -AutoSize
    } else {
        Write-Output ""
        Write-Output "No previous daily baseline found. Baseline written to: $BaselinePath"
    }
}
