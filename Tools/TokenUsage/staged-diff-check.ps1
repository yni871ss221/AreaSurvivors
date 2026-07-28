param(
    [switch]$PrintOutput
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$reportDirectory = Join-Path $projectRoot "TokenReports"
[System.IO.Directory]::CreateDirectory($reportDirectory) | Out-Null
$reportPath = Join-Path $reportDirectory ((Get-Date).ToString("yyyy-MM-dd") + ".jsonl")

$recordJson = & "$PSScriptRoot\Safe-Command.ps1" `
    -Command "git diff --cached --check" `
    -ReportPath $reportPath `
    -Json
$record = $recordJson | ConvertFrom-Json

$innerExitCode = if ($record.exit_code -eq $null) { 1 } else { [int]$record.exit_code }
Write-Output ("staged_diff_check_exit_code: {0}" -f $innerExitCode)
Write-Output ("staged_diff_check_timed_out: {0}" -f [bool]$record.timed_out)
Write-Output ("staged_diff_check_capture_path: {0}" -f [string]$record.capture_path)

if (($PrintOutput -or $innerExitCode -ne 0) -and
    -not [string]::IsNullOrWhiteSpace([string]$record.capture_path) -and
    (Test-Path -LiteralPath ([string]$record.capture_path) -PathType Leaf)) {
    $captured = Get-Content -LiteralPath ([string]$record.capture_path) -Encoding UTF8
    if ($captured.Count -gt 0) {
        Write-Output "--- staged diff check output ---"
        $captured | Write-Output
    }
}

if ([bool]$record.timed_out) {
    exit 124
}
exit $innerExitCode
