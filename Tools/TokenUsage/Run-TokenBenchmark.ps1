param(
    [string]$BaseRef = "e68ca9a",
    [string]$HeadRef = "2771c1c",
    [switch]$IncludeRtk,
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
    "git diff --stat $BaseRef..$HeadRef",
    "git diff --name-only $BaseRef..$HeadRef",
    "git diff $BaseRef..$HeadRef -- Assets/AreaSurvivors/Scenes/05_Game.unity",
    "git grep -n public -- Assets/AreaSurvivors/Scripts",
    "Get-ChildItem -Recurse Assets/AreaSurvivors | Select-Object FullName,Length",
    "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Code.Symbol -Symbol GameManager -MaxResults 10"
)

if ($IncludeRtk) {
    $rtk = "C:\Users\yni87\.local\bin\rtk.exe"
    if (Test-Path -LiteralPath $rtk) {
        $commands += @(
            "& '$rtk' git diff --stat $BaseRef..$HeadRef",
            "& '$rtk' git diff --name-only $BaseRef..$HeadRef",
            "& '$rtk' grep public Assets/AreaSurvivors/Scripts"
        )
    }
}

if ($IncludeUnity) {
    $commands += @(
        "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Compile",
        "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Console -ConsoleLevel Error -MaxResults 30",
        "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Menu -MenuPath 'Area Survivors/Reports/Scene Prefab Overview'"
    )
}

$results = @()
foreach ($command in $commands) {
    $capturePath = Join-Path ([System.IO.Path]::GetTempPath()) ("token-benchmark-" + [guid]::NewGuid().ToString("N") + ".txt")
    $escapedPath = $capturePath.Replace("'", "''")
    $script = "& { $command } *>&1 | Out-File -LiteralPath '$escapedPath' -Encoding utf8; if (`$global:LASTEXITCODE -ne `$null) { exit `$global:LASTEXITCODE }"
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command $script | Out-Null
    $exitCode = $LASTEXITCODE
    $text = if (Test-Path -LiteralPath $capturePath) { [System.IO.File]::ReadAllText($capturePath) } else { "" }
    $estimate = Get-TokenUsageEstimate -Text $text -Source $command
    $record = [pscustomobject]@{
        timestamp = (Get-Date).ToString("o")
        kind = "benchmark"
        base_ref = $BaseRef
        head_ref = $HeadRef
        command = $command
        exit_code = $exitCode
        capture_path = $capturePath
        estimate = $estimate
        advice = Get-TokenUsageAdvice -Estimate $estimate
    }
    Write-TokenUsageJsonLine -Record $record -ReportPath $ReportPath | Out-Null
    $results += $record
}

$comparison = @()
if ([string]::IsNullOrWhiteSpace($BaselinePath)) {
    $BaselinePath = Join-Path (Get-Location) "TokenReports\token-benchmark-baseline.json"
}

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
        base_ref = $BaseRef
        head_ref = $HeadRef
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
                      @{Name="lines";Expression={$_.estimate.lines}},
                      @{Name="chars";Expression={$_.estimate.chars}},
                      command |
        Format-Table -AutoSize

    if ($comparison.Count -gt 0) {
        Write-Output ""
        Write-Output "Comparison to baseline:"
        $comparison |
            Select-Object status, delta_percent, delta_tokens, baseline_tokens, current_tokens, command |
            Format-Table -AutoSize
    } else {
        Write-Output ""
        Write-Output "No previous baseline found. Baseline written to: $BaselinePath"
    }
}
