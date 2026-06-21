param(
    [int]$WarnIncreasePercent = 10,
    [switch]$IncludeUnity,
    [switch]$UpdateBaseline,
    [switch]$FailOnIncrease,
    [int]$Top = 5
)

$ErrorActionPreference = "Stop"

$benchmarkPath = Join-Path $PSScriptRoot "Run-TokenDailyHealth.ps1"
$argsForBenchmark = @{
    WarnIncreasePercent = $WarnIncreasePercent
    Json = $true
}
if ($IncludeUnity) { $argsForBenchmark.IncludeUnity = $true }
if ($UpdateBaseline) { $argsForBenchmark.UpdateBaseline = $true }

$json = & $benchmarkPath @argsForBenchmark
$result = $json | ConvertFrom-Json
$comparison = @($result.comparison)
$rows = @()

foreach ($item in $comparison) {
    $rows += [pscustomobject]@{
        status = $item.status
        delta_percent = $item.delta_percent
        delta_tokens = $item.delta_tokens
        current_tokens = $item.current_tokens
        command = $item.command
    }
}

if ($rows.Count -eq 0) {
    Write-Output "Token health: no daily baseline comparison available."
    Write-Output ("baseline_path: {0}" -f $result.baseline_path)
    exit 0
}

$increased = @($rows | Where-Object { $_.status -eq "increased" })
$improved = @($rows | Where-Object { $_.status -eq "improved" })

Write-Output ("Token health: {0} increased, {1} improved, {2} checked" -f $increased.Count, $improved.Count, $rows.Count)
$rows |
    Sort-Object @{Expression = { if ($_.status -eq "increased") { 0 } elseif ($_.status -eq "improved") { 1 } else { 2 } }}, @{Expression = "current_tokens"; Descending = $true} |
    Select-Object -First $Top |
    Select-Object status, delta_percent, delta_tokens, current_tokens, command |
    Format-Table -AutoSize

Write-Output ("daily_baseline_path: {0}" -f $result.baseline_path)

if ($FailOnIncrease -and $increased.Count -gt 0) {
    exit 2
}
