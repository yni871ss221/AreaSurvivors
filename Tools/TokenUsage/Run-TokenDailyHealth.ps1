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
    "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Git.Status",
    "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Command.Guard -CommandText 'git diff'",
    "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Code.Search -Pattern 'BuildMode' -Path Assets/AreaSurvivors/Scripts",
    "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Code.Read -Path AGENTS.md -StartLine 1 -EndLine 80",
    "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Token.Summary -Days 1 -MaxResults 5"
)

if ($IncludeUnity) {
    $commands += @(
        "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Compile",
        "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Console -ConsoleLevel Error -MaxResults 30",
        "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Search -Pattern BuildMode"
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
