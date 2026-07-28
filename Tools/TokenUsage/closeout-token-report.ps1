param(
    [string]$ReportDirectory = "TokenReports",
    [ValidateRange(1, 30)]
    [int]$Days = 1,
    [ValidateRange(1, 20)]
    [int]$Top = 8,
    [ValidateRange(1000, 1000000)]
    [int]$HighDisplayedTokens = 20000,
    [switch]$Json,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

function Get-TokenCommandFamily {
    param(
        [string]$Kind,
        [string]$Command
    )

    if ($Kind -eq "graphify") { return "graphify" }
    if ($Command -match '(?i)^git\s+diff\b') { return "git_diff" }
    if ($Command -match '(?i)^git\s+status\b') { return "git_status" }
    if ($Command -match '(?i)rg -l|rg --files') { return "file_search" }
    if ($Command -match '(?i)rg -n|rg -c|rg --count') { return "content_search" }
    if ($Command -match '(?i)Get-Content') { return "file_read" }
    if ($Command -match '(?i)unicli|Unity|Editor') { return "unity_editor" }
    if ($Kind -eq "manual_untracked_usage") { return "manual_untracked" }
    return "other"
}

function Get-CloseoutTokenAudit {
    param(
        [Parameter(Mandatory = $true)][string]$AuditReportDirectory,
        [Parameter(Mandatory = $true)][int]$AuditDays,
        [Parameter(Mandatory = $true)][int]$AuditTop,
        [Parameter(Mandatory = $true)][int]$AuditHighDisplayedTokens
    )

    $summaryJson = & "$PSScriptRoot\Get-TokenReportSummary.ps1" `
        -ReportDirectory $AuditReportDirectory `
        -Days $AuditDays `
        -Top 100000 `
        -IncludeBenchmark `
        -Json
    $summary = $summaryJson | ConvertFrom-Json
    $coverageSummary = @($summary.kind_summary | Where-Object { $_.kind -eq "token_coverage_snapshot" } | Select-Object -First 1)
    $coverageSnapshotRecords = if ($coverageSummary.Count -gt 0) { [int]$coverageSummary[0].records } else { 0 }
    $records = @($summary.top_commands | Where-Object {
        $_.kind -in @("safe_command", "graphify", "manual_untracked_usage", "daily_health")
    })

    $familyRows = New-Object System.Collections.ArrayList
    foreach ($group in @($records | Group-Object {
        Get-TokenCommandFamily -Kind ([string]$_.kind) -Command ([string]$_.command)
    })) {
        $measuredGroup = @($group.Group | Where-Object { $_.displayed_tokens -ne $null })
        [void]$familyRows.Add([pscustomobject]@{
            family = $group.Name
            records = $group.Count
            capture_tokens = [int](($group.Group | Measure-Object -Property capture_tokens -Sum).Sum)
            displayed_tokens = [int](($measuredGroup | Measure-Object -Property displayed_tokens -Sum).Sum)
            measurement_gaps = @($group.Group | Where-Object { $_.measurement_status -ne "measured_display" }).Count
            failed = @($group.Group | Where-Object {
                $_.timed_out -or ($_.exit_code -ne $null -and [int]$_.exit_code -ne 0)
            }).Count
        })
    }

    $measuredRecords = @($records | Where-Object { $_.measurement_status -eq "measured_display" })
    $legacyGapRecords = @($records | Where-Object { $_.measurement_status -eq "legacy_capture_only" })
    $currentGapRecords = @($records | Where-Object { $_.measurement_status -eq "current_schema_gap" })
    $gapRecords = @($records | Where-Object { $_.measurement_status -ne "measured_display" })
    $displayedTokens = [int](($measuredRecords | Measure-Object -Property displayed_tokens -Sum).Sum)
    $captureTokens = [int](($records | Measure-Object -Property capture_tokens -Sum).Sum)
    $gapCaptureTokens = [int](($gapRecords | Measure-Object -Property capture_tokens -Sum).Sum)
    $failedRecords = @($records | Where-Object {
        $_.timed_out -or ($_.exit_code -ne $null -and [int]$_.exit_code -ne 0)
    })
    $measurementCoverage = if ($records.Count -eq 0) {
        100.0
    } else {
        [math]::Round(($measuredRecords.Count / [double]$records.Count) * 100.0, 1)
    }

    $repeatRows = @($records |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.command) } |
        Group-Object command |
        Where-Object { $_.Count -gt 1 } |
        ForEach-Object {
            [pscustomobject]@{
                command = $_.Name
                count = $_.Count
                capture_tokens = [int](($_.Group | Measure-Object -Property capture_tokens -Sum).Sum)
                displayed_tokens = [int](($_.Group | Where-Object { $_.displayed_tokens -ne $null } | Measure-Object -Property displayed_tokens -Sum).Sum)
            }
        } |
        Sort-Object @{ Expression = "count"; Descending = $true },
                    @{ Expression = "capture_tokens"; Descending = $true })

    $recommendations = New-Object System.Collections.ArrayList
    if ($legacyGapRecords.Count -gt 0) {
        [void]$recommendations.Add([pscustomobject]@{
            code = "legacy-measurement-gap"
            message = "Historical records have capture-only estimates. Do not treat their raw capture tokens as billed tokens."
            evidence = ("records={0}" -f $legacyGapRecords.Count)
        })
    }
    if ($currentGapRecords.Count -gt 0) {
        [void]$recommendations.Add([pscustomobject]@{
            code = "current-measurement-gap"
            message = "Current schema records are missing displayed-token values. Fix the responsible logger and its self-test before relying on closeout totals."
            evidence = ("records={0}, capture_tokens={1}" -f
                $currentGapRecords.Count,
                [int](($currentGapRecords | Measure-Object -Property capture_tokens -Sum).Sum))
        })
    }
    if ($coverageSnapshotRecords -eq 0) {
        [void]$recommendations.Add([pscustomobject]@{
            code = "missing-session-coverage"
            message = "No session coverage snapshot was recorded. Unwrapped tools, tool metadata, chat, and reasoning remain outside measured command-output totals."
            evidence = "coverage_snapshot_records=0"
        })
    }
    $fileReadRow = @($familyRows | Where-Object { $_.family -eq "file_read" } | Select-Object -First 1)
    if ($fileReadRow.Count -gt 0 -and $captureTokens -gt 0 -and
        ([double]$fileReadRow[0].capture_tokens / [double]$captureTokens) -ge 0.5) {
        [void]$recommendations.Add([pscustomobject]@{
            code = "reduce-file-read"
            message = "File reads dominate capture volume. Prefer Graphify paths, focused symbol ranges, compact ctx, and cached wrapper contracts."
            evidence = ("capture_tokens={0}, share={1:P1}" -f
                $fileReadRow[0].capture_tokens,
                ([double]$fileReadRow[0].capture_tokens / [double]$captureTokens))
        })
    }
    $gitDiffRow = @($familyRows | Where-Object { $_.family -eq "git_diff" } | Select-Object -First 1)
    if ($gitDiffRow.Count -gt 0 -and $gitDiffRow[0].capture_tokens -ge 5000) {
        [void]$recommendations.Add([pscustomobject]@{
            code = "reduce-git-diff"
            message = "Use safe-diff summary and targeted scoped diff checks before any content diff."
            evidence = ("capture_tokens={0}" -f $gitDiffRow[0].capture_tokens)
        })
    }
    $topRepeat = @($repeatRows | Select-Object -First 1)
    if ($topRepeat.Count -gt 0 -and $topRepeat[0].count -ge 5) {
        [void]$recommendations.Add([pscustomobject]@{
            code = "deduplicate-command"
            message = "A command was repeated at least five times. Batch it, cache its contract/result, or add a dedicated reporter."
            evidence = ("count={0}, capture_tokens={1}, command={2}" -f
                $topRepeat[0].count,
                $topRepeat[0].capture_tokens,
                $topRepeat[0].command)
        })
    }
    if ($failedRecords.Count -ge 2) {
        [void]$recommendations.Add([pscustomobject]@{
            code = "reduce-command-failures"
            message = "Multiple command failures were recorded. Promote the repeated boundary into a validator or wrapper before more task work."
            evidence = ("failed={0}" -f $failedRecords.Count)
        })
    }
    if ($displayedTokens -ge $AuditHighDisplayedTokens) {
        [void]$recommendations.Add([pscustomobject]@{
            code = "high-visible-output"
            message = "Measured displayed output exceeded the closeout threshold. Tighten output caps for the highest displayed family before the next session."
            evidence = ("displayed_tokens={0}, threshold={1}" -f $displayedTokens, $AuditHighDisplayedTokens)
        })
    }

    $status = if ($displayedTokens -ge $AuditHighDisplayedTokens) {
        "high"
    } elseif ($measurementCoverage -lt 80.0) {
        "measurement_incomplete"
    } else {
        "normal"
    }

    return [pscustomobject]@{
        timestamp = (Get-Date).ToString("o")
        status = $status
        days = $AuditDays
        records = $records.Count
        capture_estimated_tokens = $captureTokens
        displayed_estimated_tokens = $displayedTokens
        displayed_measured_records = $measuredRecords.Count
        measurement_gap_records = $gapRecords.Count
        legacy_measurement_gap_records = $legacyGapRecords.Count
        current_schema_gap_records = $currentGapRecords.Count
        coverage_snapshot_records = $coverageSnapshotRecords
        measurement_gap_capture_tokens = $gapCaptureTokens
        measurement_coverage_percent = $measurementCoverage
        failed_records = $failedRecords.Count
        blocked_records = @($records | Where-Object { $_.blocked }).Count
        top_families = @($familyRows |
            Sort-Object @{ Expression = "displayed_tokens"; Descending = $true },
                        @{ Expression = "capture_tokens"; Descending = $true } |
            Select-Object -First $AuditTop)
        top_repeated_commands = @($repeatRows | Select-Object -First $AuditTop)
        recommendations = @($recommendations.ToArray())
    }
}

function Invoke-CloseoutTokenReportSelfTest {
    $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("closeout-token-report-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
    try {
        $today = Get-Date
        $dailyPath = Join-Path $fixtureRoot ($today.ToString("yyyy-MM-dd") + ".jsonl")
        $records = @(
            [ordered]@{
                timestamp = $today.ToString("o")
                kind = "safe_command"
                schema_version = 2
                command = "Get-Content -LiteralPath 'ctx/current.md'"
                exit_code = 0
                blocked = $false
                estimate = [ordered]@{ estimated_tokens = 400; risk = "low" }
                visibility = [ordered]@{
                    capture_estimated_tokens = 400
                    displayed_capture_estimated_tokens = 100
                }
            },
            [ordered]@{
                timestamp = $today.ToString("o")
                kind = "safe_command"
                command = "git diff"
                exit_code = 0
                blocked = $false
                estimate = [ordered]@{ estimated_tokens = 800; risk = "low" }
            },
            [ordered]@{
                timestamp = $today.ToString("o")
                kind = "safe_command"
                schema_version = 2
                command = "Get-Content -LiteralPath 'missing-visibility.md'"
                exit_code = 0
                blocked = $false
                estimate = [ordered]@{ estimated_tokens = 50; risk = "low" }
            }
        )
        $dailyLines = @($records | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 8 })
        [System.IO.File]::WriteAllLines($dailyPath, [string[]]$dailyLines, [System.Text.UTF8Encoding]::new($false))

        $graphPath = Join-Path $fixtureRoot "graphify-pilot-usage.jsonl"
        $graphRecord = [ordered]@{
            timestamp = $today.ToString("o")
            graphify_version = "0.9.26"
            usage_category = "production"
            action = "Affected"
            source = "Example"
            target = ""
            exit_code = 0
            estimated_output_tokens = 200
            displayed_estimated_tokens = 50
        }
        [System.IO.File]::WriteAllText(
            $graphPath,
            (($graphRecord | ConvertTo-Json -Compress -Depth 8) + [Environment]::NewLine),
            [System.Text.UTF8Encoding]::new($false)
        )

        $audit = Get-CloseoutTokenAudit `
            -AuditReportDirectory $fixtureRoot `
            -AuditDays 1 `
            -AuditTop 5 `
            -AuditHighDisplayedTokens 1000
        if ($audit.records -ne 4) { throw "SelfTest expected 4 records, got $($audit.records)." }
        if ($audit.capture_estimated_tokens -ne 1450) { throw "SelfTest capture token mismatch." }
        if ($audit.displayed_estimated_tokens -ne 150) { throw "SelfTest displayed token mismatch." }
        if ($audit.legacy_measurement_gap_records -ne 1) { throw "SelfTest legacy measurement gap mismatch." }
        if ($audit.current_schema_gap_records -ne 1) { throw "SelfTest current measurement gap mismatch." }
        if (@($audit.recommendations | Where-Object { $_.code -eq "current-measurement-gap" }).Count -ne 1) {
            throw "SelfTest current measurement recommendation is missing."
        }
        if (@($audit.recommendations | Where-Object { $_.code -eq "missing-session-coverage" }).Count -ne 1) {
            throw "SelfTest coverage recommendation is missing."
        }
        Write-Output "closeout_token_report_self_test: passed"
    } finally {
        if (Test-Path -LiteralPath $fixtureRoot) {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        }
    }
}

if ($SelfTest) {
    Invoke-CloseoutTokenReportSelfTest
    exit 0
}

$auditResult = Get-CloseoutTokenAudit `
    -AuditReportDirectory $ReportDirectory `
    -AuditDays $Days `
    -AuditTop $Top `
    -AuditHighDisplayedTokens $HighDisplayedTokens

if ($Json) {
    $auditResult | ConvertTo-Json -Depth 8
    exit 0
}

Write-Output ("closeout_token_audit: {0}" -f $auditResult.status)
Write-Output ("records: {0}" -f $auditResult.records)
Write-Output ("capture_estimated_tokens: {0}" -f $auditResult.capture_estimated_tokens)
Write-Output ("displayed_estimated_tokens: {0}" -f $auditResult.displayed_estimated_tokens)
Write-Output ("measurement_coverage_percent: {0}" -f $auditResult.measurement_coverage_percent)
Write-Output ("measurement_gap_records: {0}" -f $auditResult.measurement_gap_records)
Write-Output ("legacy_measurement_gap_records: {0}" -f $auditResult.legacy_measurement_gap_records)
Write-Output ("current_schema_gap_records: {0}" -f $auditResult.current_schema_gap_records)
Write-Output ("coverage_snapshot_records: {0}" -f $auditResult.coverage_snapshot_records)
Write-Output ("failed_records: {0}" -f $auditResult.failed_records)
Write-Output ("blocked_records: {0}" -f $auditResult.blocked_records)
Write-Output ""
Write-Output "Top token families:"
$auditResult.top_families |
    Select-Object family, records, displayed_tokens, capture_tokens, measurement_gaps, failed |
    Format-Table -AutoSize
Write-Output ""
Write-Output "Recommendations:"
if ($auditResult.recommendations.Count -eq 0) {
    Write-Output "- none"
} else {
    foreach ($recommendation in $auditResult.recommendations) {
        Write-Output ("- [{0}] {1} ({2})" -f $recommendation.code, $recommendation.message, $recommendation.evidence)
    }
}
