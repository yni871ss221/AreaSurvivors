param([switch]$IncludeUnity, [switch]$RunUnity, [int]$ArchiveOlderThanDays = 3)

$ErrorActionPreference = "Stop"
Write-Output "[end-token-check] token report ignore"
git check-ignore -q TokenReports/
if ($LASTEXITCODE -eq 0) { Write-Output "TokenReports/ is ignored." } else { Write-Warning "TokenReports/ is not ignored by git." }
Write-Output ""
Write-Output "[end-token-check] archive old token reports"
& "$PSScriptRoot\archive-token-reports.ps1" -OlderThanDays $ArchiveOlderThanDays
Write-Output ""
if ($RunUnity -or $IncludeUnity) {
    Write-Output "[end-token-check] compile"
    & "$PSScriptRoot\safe-unity.ps1" -Action Compile
    Write-Output ""
    Write-Output "[end-token-check] console errors"
    & "$PSScriptRoot\safe-unity.ps1" -Action ConsoleErrors -MaxCount 30
    Write-Output ""
} else {
    Write-Output "[end-token-check] unity checks skipped; pass -RunUnity or -IncludeUnity when needed."
    Write-Output ""
}
Write-Output "[end-token-check] token health"
& "$PSScriptRoot\token-health.ps1" -IncludeUnity:$IncludeUnity -Top 5
Write-Output ""
Write-Output "[end-token-check] report summary"
& "$PSScriptRoot\token-report-summary.ps1" -Days 1 -Kind safe_command,daily_health -Top 8
