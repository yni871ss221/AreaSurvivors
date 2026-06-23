param(
    [double]$StartPercent = -1,
    [double]$CurrentPercent = -1,
    [int]$BudgetTokens = 0,
    [string]$Note = "",
    [switch]$Save,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\TokenUsageCommon.ps1"

function Get-LatestTokenStartMarker {
    $reportRoot = Join-Path (Get-Location) "TokenReports"
    if (-not (Test-Path -LiteralPath $reportRoot)) { return $null }

    $latest = $null
    foreach ($file in Get-ChildItem -LiteralPath $reportRoot -Filter "*.jsonl" -File) {
        foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding UTF8) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try {
                $record = $line | ConvertFrom-Json
                if ($record.kind -ne "token_start_marker") { continue }
                if ($latest -eq $null -or [DateTime]::Parse($record.timestamp) -gt [DateTime]::Parse($latest.timestamp)) {
                    $latest = $record
                }
            } catch {
                continue
            }
        }
    }
    return $latest
}

function Get-TokenRecordsSince {
    param([DateTime]$Since)

    $reportRoot = Join-Path (Get-Location) "TokenReports"
    if (-not (Test-Path -LiteralPath $reportRoot)) { return @() }

    $records = @()
    foreach ($file in Get-ChildItem -LiteralPath $reportRoot -Filter "*.jsonl" -File) {
        foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding UTF8) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try {
                $record = $line | ConvertFrom-Json
                if ([string]::IsNullOrWhiteSpace([string]$record.timestamp)) { continue }
                if ([DateTime]::Parse($record.timestamp) -lt $Since) { continue }
                $tokens = 0
                if ($record.estimate -and $record.estimate.estimated_tokens -ne $null) {
                    $tokens = [int]$record.estimate.estimated_tokens
                }
                $records += [pscustomobject]@{
                    kind = [string]$record.kind
                    tokens = $tokens
                }
            } catch {
                continue
            }
        }
    }
    return $records
}

$latestMarker = Get-LatestTokenStartMarker
$latestMarkerHasCoverageStart = $latestMarker -ne $null -and $latestMarker.PSObject.Properties.Name -contains "coverage_start" -and $latestMarker.coverage_start -ne $null
if ($latestMarkerHasCoverageStart) {
    if ($StartPercent -lt 0 -and $latestMarker.coverage_start.PSObject.Properties.Name -contains "ui_percent" -and $latestMarker.coverage_start.ui_percent -ne $null) {
        $StartPercent = [double]$latestMarker.coverage_start.ui_percent
    }
    if ($BudgetTokens -le 0 -and $latestMarker.coverage_start.PSObject.Properties.Name -contains "budget_tokens" -and $latestMarker.coverage_start.budget_tokens -ne $null) {
        $BudgetTokens = [int]$latestMarker.coverage_start.budget_tokens
    }
}

$summaryJson = & "$PSScriptRoot\Get-TokenReportSummary.ps1" -SinceLastStart -Json
$summary = $summaryJson | ConvertFrom-Json
$recordedTokens = [int]$summary.total_estimated_tokens
$sinceDate = [DateTime]::Parse($summary.since)
$recordsSince = @(Get-TokenRecordsSince -Since $sinceDate)
$manualTokens = 0
foreach ($record in $recordsSince) {
    if ($record.kind -eq "manual_untracked_usage") {
        $manualTokens += [int]$record.tokens
    }
}
$commandTokens = [int][math]::Max(0, $recordedTokens - $manualTokens)

$hasUiPercent = $StartPercent -ge 0 -and $CurrentPercent -ge 0
$deltaPercent = if ($hasUiPercent) { [math]::Max(0.0, [double]($CurrentPercent - $StartPercent)) } else { $null }
$uiEstimatedTokens = $null
$untrackedEstimatedTokens = $null

if ($hasUiPercent -and $BudgetTokens -gt 0) {
    $uiEstimatedTokens = [int][math]::Ceiling($BudgetTokens * ($deltaPercent / 100.0))
    $untrackedEstimatedTokens = [int][math]::Max(0, $uiEstimatedTokens - $recordedTokens)
}

$coverage = [pscustomobject]@{
    timestamp = (Get-Date).ToString("o")
    kind = "token_coverage_snapshot"
    since = $summary.since
    records = $summary.records
    recorded_estimated_tokens = $recordedTokens
    command_recorded_estimated_tokens = $commandTokens
    manual_recorded_estimated_tokens = $manualTokens
    ui_start_percent = if ($StartPercent -ge 0) { $StartPercent } else { $null }
    ui_current_percent = if ($CurrentPercent -ge 0) { $CurrentPercent } else { $null }
    ui_delta_percent = $deltaPercent
    budget_tokens = if ($BudgetTokens -gt 0) { $BudgetTokens } else { $null }
    ui_estimated_tokens = $uiEstimatedTokens
    untracked_estimated_tokens = $untrackedEstimatedTokens
    untracked_after_manual_estimated_tokens = $untrackedEstimatedTokens
    start_marker_note = if ($latestMarkerHasCoverageStart -and $latestMarker.coverage_start.PSObject.Properties.Name -contains "note") { $latestMarker.coverage_start.note } else { "" }
    note = $Note
}

if ($Save) {
    $record = [pscustomobject]@{
        timestamp = $coverage.timestamp
        kind = "token_coverage_snapshot"
        command = "session-coverage"
        shell = "powershell"
        exit_code = 0
        capture_path = ""
        warn_tokens = 0
        block_tokens = 0
        blocked = $false
        estimate = [pscustomobject]@{
            source = "session-coverage"
            bytes = 0
            chars = 0
            lines = 0
            words = 0
            estimated_tokens = 0
            risk = "low"
        }
        coverage = $coverage
        advice = "Compare Codex UI usage delta with recorded TokenReports to estimate untracked context/tool/chat usage."
    }
    $coverage | Add-Member -NotePropertyName report_path -NotePropertyValue (Write-TokenUsageJsonLine -Record $record)
}

if ($Json) {
    $coverage | ConvertTo-Json -Depth 8
    exit 0
}

Write-Output ("Token coverage since: {0}" -f $coverage.since)
Write-Output ("recorded_estimated_tokens: {0}" -f $coverage.recorded_estimated_tokens)
Write-Output ("command_recorded_estimated_tokens: {0}" -f $coverage.command_recorded_estimated_tokens)
Write-Output ("manual_recorded_estimated_tokens: {0}" -f $coverage.manual_recorded_estimated_tokens)
Write-Output ("records: {0}" -f $coverage.records)
if ($hasUiPercent) {
    Write-Output ("ui_delta_percent: {0:N2}%" -f $coverage.ui_delta_percent)
} else {
    Write-Output "ui_delta_percent: not provided; pass -StartPercent and -CurrentPercent to estimate untracked usage."
}
if ($BudgetTokens -gt 0 -and $hasUiPercent) {
    Write-Output ("ui_estimated_tokens: {0}" -f $coverage.ui_estimated_tokens)
    Write-Output ("untracked_estimated_tokens: {0}" -f $coverage.untracked_estimated_tokens)
} else {
    Write-Output "untracked_estimated_tokens: not calculated; pass -BudgetTokens with UI percent values."
}
if (-not [string]::IsNullOrWhiteSpace($Note)) {
    Write-Output ("note: {0}" -f $Note)
}
if ($Save) {
    Write-Output ("saved_to: {0}" -f $coverage.report_path)
}
