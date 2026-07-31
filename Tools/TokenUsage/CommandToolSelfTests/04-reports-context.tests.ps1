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
        "measurement_coverage",
        "--output-json"
    )) {
    if (-not $summaryText.Contains($sentinel)) {
        throw "Token summary coverage sentinel is missing: $sentinel"
    }
}

$tokenCommonText = [System.IO.File]::ReadAllText(
    (Join-Path $toolsRoot "TokenUsageCommon.ps1")
)
foreach ($sentinel in @(
        "AreaSurvivors.TokenReportWriter.",
        "System.Threading.Mutex",
        "AppendAllText"
    )) {
    if (-not $tokenCommonText.Contains($sentinel)) {
        throw "TokenReports atomic writer sentinel is missing: $sentinel"
    }
}

$concurrencyRoot = Join-Path (
    [System.IO.Path]::GetTempPath()
) ("area-token-writer-test-" + [guid]::NewGuid().ToString("N"))
$concurrencyReport = Join-Path $concurrencyRoot "concurrent.jsonl"
$writerJobs = @()
try {
    New-Item -ItemType Directory -Path $concurrencyRoot -Force | Out-Null
    foreach ($writerId in 1..4) {
        $writerJobs += Start-Job -ArgumentList @(
            (Join-Path $toolsRoot "TokenUsageCommon.ps1"),
            $concurrencyReport,
            $writerId
        ) -ScriptBlock {
            param($CommonPath, $ReportPath, $WriterId)
            . $CommonPath
            foreach ($entryId in 1..12) {
                $record = [pscustomobject]@{
                    timestamp = (Get-Date).ToString("o")
                    kind = "atomic_writer_self_test"
                    command = "writer=$WriterId entry=$entryId"
                }
                Write-TokenUsageJsonLine `
                    -Record $record `
                    -ReportPath $ReportPath |
                    Out-Null
            }
        }
    }
    $writerJobs | Wait-Job -Timeout 30 | Out-Null
    if (@($writerJobs | Where-Object State -ne "Completed").Count -gt 0) {
        throw "TokenReports concurrent writer self-test timed out."
    }
    $writerJobs | Receive-Job | Out-Null
    $concurrencyLines = [System.IO.File]::ReadAllLines(
        $concurrencyReport,
        [System.Text.Encoding]::UTF8
    )
    if ($concurrencyLines.Count -ne 48) {
        throw "TokenReports concurrent writer record count mismatch."
    }
    foreach ($line in $concurrencyLines) {
        $record = $line | ConvertFrom-Json
        if ($record.kind -ne "atomic_writer_self_test") {
            throw "TokenReports concurrent writer emitted an invalid record."
        }
    }
} finally {
    $writerJobs | Remove-Job -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $concurrencyRoot) {
        Remove-Item -LiteralPath $concurrencyRoot -Recurse -Force
    }
}

Write-Output "command_tool_test_module: reports-context passed"
