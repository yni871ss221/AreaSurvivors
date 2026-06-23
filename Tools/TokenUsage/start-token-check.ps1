param(
    [switch]$IncludeUnity,
    [double]$UiPercent = -1,
    [int]$BudgetTokens = 0,
    [string]$Note = ""
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\TokenUsageCommon.ps1"

$marker = [pscustomobject]@{
    timestamp = (Get-Date).ToString("o")
    kind = "token_start_marker"
    command = "start-token-check"
    shell = "powershell"
    exit_code = 0
    capture_path = ""
    warn_tokens = 0
    block_tokens = 0
    blocked = $false
    estimate = [pscustomobject]@{
        source = "start-token-check"
        bytes = 0
        chars = 0
        lines = 0
        words = 0
        estimated_tokens = 0
        risk = "low"
    }
    coverage_start = [pscustomobject]@{
        ui_percent = if ($UiPercent -ge 0) { $UiPercent } else { $null }
        budget_tokens = if ($BudgetTokens -gt 0) { $BudgetTokens } else { $null }
        note = $Note
    }
    advice = "Marker for token-report-summary -SinceLastStart."
}
Write-TokenUsageJsonLine -Record $marker | Out-Null

if ($UiPercent -ge 0) {
    Write-Output ("[start-token-check] ui_percent: {0:N2}%" -f $UiPercent)
} else {
    Write-Output "[start-token-check] ui_percent not provided; pass -UiPercent <Codex UI usage percent> for coverage estimates."
}
if ($BudgetTokens -gt 0) {
    Write-Output ("[start-token-check] budget_tokens: {0}" -f $BudgetTokens)
}
if (-not [string]::IsNullOrWhiteSpace($Note)) {
    Write-Output ("[start-token-check] note: {0}" -f $Note)
}
Write-Output ""

Write-Output "[start-token-check] status"
& "$PSScriptRoot\safe-status.ps1"
Write-Output ""
Write-Output "[start-token-check] token health"
& "$PSScriptRoot\token-health.ps1" -IncludeUnity:$IncludeUnity
