param([switch]$IncludeUnity)

$ErrorActionPreference = "Stop"
Write-Output "[end-token-check] compile"
& "$PSScriptRoot\safe-unity.ps1" -Action Compile
Write-Output ""
Write-Output "[end-token-check] console errors"
& "$PSScriptRoot\safe-unity.ps1" -Action ConsoleErrors -MaxCount 30
Write-Output ""
Write-Output "[end-token-check] token health"
& "$PSScriptRoot\token-health.ps1" -IncludeUnity:$IncludeUnity
Write-Output ""
Write-Output "[end-token-check] report summary"
& "$PSScriptRoot\token-report-summary.ps1" -Days 1 -Top 8
