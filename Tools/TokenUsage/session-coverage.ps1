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

$summaryJson = & "$PSScriptRoot\Get-TokenReportSummary.ps1" -SinceLastStart -Json
$summary = $summaryJson | ConvertFrom-Json
$recordedTokens = [int]$summary.total_estimated_tokens

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
    ui_start_percent = if ($StartPercent -ge 0) { $StartPercent } else { $null }
    ui_current_percent = if ($CurrentPercent -ge 0) { $CurrentPercent } else { $null }
    ui_delta_percent = $deltaPercent
    budget_tokens = if ($BudgetTokens -gt 0) { $BudgetTokens } else { $null }
    ui_estimated_tokens = $uiEstimatedTokens
    untracked_estimated_tokens = $untrackedEstimatedTokens
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
