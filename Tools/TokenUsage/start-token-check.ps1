param([switch]$IncludeUnity)

$ErrorActionPreference = "Stop"
Write-Output "[start-token-check] status"
& "$PSScriptRoot\safe-status.ps1"
Write-Output ""
Write-Output "[start-token-check] token health"
& "$PSScriptRoot\token-health.ps1" -IncludeUnity:$IncludeUnity
