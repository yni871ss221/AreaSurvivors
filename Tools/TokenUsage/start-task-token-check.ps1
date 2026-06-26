param(
    [Parameter(Mandatory = $true)][string]$Task,
    [double]$UiPercent = -1,
    [int]$BudgetTokens = 0,
    [switch]$IncludeUnity
)

$ErrorActionPreference = "Stop"

Write-Output "[start-task-token-check] routed rules"
& "$PSScriptRoot\rule-router.ps1" -Task $Task
Write-Output ""

$startArgs = @("-Note", $Task)
if ($UiPercent -ge 0) { $startArgs += @("-UiPercent", $UiPercent) }
if ($BudgetTokens -gt 0) { $startArgs += @("-BudgetTokens", $BudgetTokens) }
if ($IncludeUnity) { $startArgs += "-IncludeUnity" }

& "$PSScriptRoot\start-token-check.ps1" @startArgs
