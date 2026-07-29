$ErrorActionPreference = "Stop"

$toolsRoot = Split-Path $PSScriptRoot -Parent
$projectRoot = Split-Path (Split-Path $toolsRoot -Parent) -Parent

$selfTests = @(
    @{
        Script = "project-cleanliness-report.ps1"
        Marker = "project_cleanliness_self_test: passed"
    },
    @{
        Script = "performance-session-report.ps1"
        Marker = "performance_session_report_self_test: passed"
    },
    @{
        Script = "performance-stage-detail-report.ps1"
        Marker = "performance_stage_detail_report_self_test: passed"
    },
    @{
        Script = "Get-TokenReportSummary.ps1"
        Marker = "self-test passed"
    },
    @{
        Script = "closeout-token-report.ps1"
        Marker = "closeout_token_report_self_test: passed"
    }
)

foreach ($test in $selfTests) {
    $path = Join-Path $toolsRoot $test.Script
    $command = Get-Command $path
    if (-not $command.Parameters.ContainsKey("SelfTest")) {
        throw "$($test.Script) is missing the SelfTest parameter."
    }
    $output = @(& $path -SelfTest) -join "`n"
    if (-not $output.Contains($test.Marker)) {
        throw "$($test.Script) did not emit its success marker."
    }
}

$contextGuard = Join-Path $toolsRoot "current-context-guard.ps1"
$contextOutput = @(
    & $contextGuard `
        -ProjectRoot $projectRoot
) -join "`n"
$contextResult = $contextOutput | ConvertFrom-Json
if ($contextResult.status -ne "ok") {
    throw "current-context-guard did not report status ok."
}

$summaryText = [System.IO.File]::ReadAllText(
    (Join-Path $toolsRoot "Get-TokenReportSummary.ps1")
) + [System.IO.File]::ReadAllText(
    (Join-Path $toolsRoot "token-report-index.py")
)
foreach ($sentinel in @(
        "displayed_estimated_tokens",
        "capture_estimated_tokens",
        "latest_start_ui_percent",
        "measurement_coverage"
    )) {
    if (-not $summaryText.Contains($sentinel)) {
        throw "Token summary coverage sentinel is missing: $sentinel"
    }
}

Write-Output "command_tool_test_module: reports-context passed"
