param(
    [Alias("Path")]
    [string]$ReportDirectory = "TokenReports",
    [int]$Days = 7,
    [string]$Since = "",
    [string[]]$Kind = @(),
    [int]$Top = 10,
    [int]$Recent = 0,
    [switch]$SinceLastStart,
    [switch]$FailedOnly,
    [switch]$IncludeBenchmark,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$explicitReportFile = $false
if (-not (Test-Path -LiteralPath $ReportDirectory)) {
    Write-Output "No TokenReports directory found."
    exit 0
}
$reportItem = Get-Item -LiteralPath $ReportDirectory
if (-not $reportItem.PSIsContainer) {
    $explicitReportFile = $true
}

$sinceDate = if ([string]::IsNullOrWhiteSpace($Since)) {
    (Get-Date).Date.AddDays(-[math]::Max(0, $Days - 1))
} else {
    [DateTime]::Parse($Since)
}
$kindSet = @{}
foreach ($item in $Kind) {
    foreach ($part in ($item -split ",")) {
        $trimmed = $part.Trim()
        if (-not [string]::IsNullOrWhiteSpace($trimmed)) { $kindSet[$trimmed] = $true }
    }
}
$records = New-Object System.Collections.ArrayList
$reportFiles = if ($explicitReportFile) {
    @($reportItem)
} else {
    @(Get-ChildItem -LiteralPath $ReportDirectory -Filter "*.jsonl" -File | Where-Object { $_.LastWriteTime -ge $sinceDate })
}
foreach ($file in $reportFiles) {
    foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding UTF8) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try {
            $record = $line | ConvertFrom-Json
            $recordTimestamp = [string]$record.timestamp
            if (-not [string]::IsNullOrWhiteSpace($recordTimestamp)) {
                try {
                    if ([DateTime]::Parse($recordTimestamp) -lt $sinceDate) { continue }
                } catch {
                    continue
                }
            }
            $recordKind = [string]$record.kind
            if ([string]::IsNullOrWhiteSpace($recordKind) -and
                $record.PSObject.Properties.Name -contains "graphify_version" -and
                $record.PSObject.Properties.Name -contains "action") {
                $recordKind = "graphify"
            }
            if ([string]::IsNullOrWhiteSpace($recordKind)) {
                $recordKind = "unrecognized_schema"
            }
            $schemaVersion = if ($record.PSObject.Properties.Name -contains "schema_version" -and $record.schema_version -ne $null) {
                [int]$record.schema_version
            } else {
                0
            }

            $captureTokens = 0
            if ($record.estimate -and $record.estimate.estimated_tokens -ne $null) {
                $captureTokens = [int]$record.estimate.estimated_tokens
            } elseif ($recordKind -eq "graphify" -and $record.estimated_output_tokens -ne $null) {
                $captureTokens = [int]$record.estimated_output_tokens
            }
            $displayedTokens = $null
            $measurementStatus = "legacy_capture_only"
            if ($record.PSObject.Properties.Name -contains "visibility" -and
                $record.visibility -ne $null -and
                $record.visibility.displayed_capture_estimated_tokens -ne $null) {
                $displayedTokens = [int]$record.visibility.displayed_capture_estimated_tokens
                $measurementStatus = "measured_display"
            } elseif ($recordKind -eq "graphify" -and
                $record.PSObject.Properties.Name -contains "displayed_estimated_tokens" -and
                $record.displayed_estimated_tokens -ne $null) {
                $displayedTokens = [int]$record.displayed_estimated_tokens
                $measurementStatus = "measured_display"
            } elseif ($recordKind -eq "safe_command" -and $schemaVersion -ge 2) {
                $measurementStatus = "current_schema_gap"
            }
            $commandLabel = [string]$record.command
            if ([string]::IsNullOrWhiteSpace($commandLabel) -and $recordKind -eq "graphify") {
                $commandLabel = ("Graphify {0} {1} {2}" -f $record.action, $record.source, $record.target).Trim()
            }
            [void]$records.Add([pscustomobject]@{
                timestamp = $recordTimestamp
                kind = $recordKind
                command = $commandLabel
                caller_script = [string]$record.caller_script
                schema_version = $schemaVersion
                tokens = $captureTokens
                capture_tokens = $captureTokens
                displayed_tokens = $displayedTokens
                measurement_status = $measurementStatus
                risk = if ($record.estimate) { $record.estimate.risk } else { "" }
                blocked = [bool]$record.blocked
                exit_code = $record.exit_code
                timeout_seconds = $record.timeout_seconds
                timed_out = [bool]$record.timed_out
                capture_path = $record.capture_path
                report_file = $file.Name
            })
        } catch {
            [void]$records.Add([pscustomobject]@{
                timestamp = ""
                kind = "parse_error"
                command = $file.Name
                caller_script = ""
                tokens = 0
                capture_tokens = 0
                displayed_tokens = $null
                measurement_status = "parse_error"
                risk = "unknown"
                blocked = $false
                exit_code = $null
                timeout_seconds = $null
                timed_out = $false
                capture_path = ""
                report_file = $file.Name
            })
        }
    }
}

if ($SinceLastStart) {
    $latestMarker = $records |
        Where-Object { $_.kind -eq "token_start_marker" -and -not [string]::IsNullOrWhiteSpace([string]$_.timestamp) } |
        Sort-Object timestamp |
        Select-Object -Last 1
    if ($latestMarker -ne $null) {
        $sinceDate = [DateTime]::Parse($latestMarker.timestamp)
        $records = @($records | Where-Object {
            -not [string]::IsNullOrWhiteSpace([string]$_.timestamp) -and [DateTime]::Parse($_.timestamp) -ge $sinceDate
        })
    }
}

if (-not $IncludeBenchmark) {
    $records = @($records | Where-Object { $_.kind -ne "benchmark" -and $_.kind -ne "token_start_marker" })
}
if ($kindSet.Count -gt 0) {
    $records = @($records | Where-Object { $kindSet.ContainsKey([string]$_.kind) })
}
if ($FailedOnly) {
    $records = @($records | Where-Object { $_.kind -eq "parse_error" -or $_.timed_out -or ($_.exit_code -ne $null -and [int]$_.exit_code -ne 0) })
}
if ($Recent -gt 0) {
    $records = @($records | Sort-Object timestamp | Select-Object -Last $Recent)
}

$totalTokens = ($records | Measure-Object -Property tokens -Sum).Sum
$displayMeasuredRecords = @($records | Where-Object { $_.measurement_status -eq "measured_display" })
$totalDisplayedTokens = ($displayMeasuredRecords | Measure-Object -Property displayed_tokens -Sum).Sum
$captureOnlyRecords = @($records | Where-Object { $_.measurement_status -eq "legacy_capture_only" })
$summary = [pscustomobject]@{
    report_directory = if ($explicitReportFile) { (Resolve-Path -LiteralPath $reportItem.DirectoryName).Path } else { (Resolve-Path -LiteralPath $ReportDirectory).Path }
    report_file = if ($explicitReportFile) { $reportItem.Name } else { "" }
    days = $Days
    since = $sinceDate.ToString("o")
    kinds = @($kindSet.Keys)
    recent = $Recent
    since_last_start = [bool]$SinceLastStart
    failed_only = [bool]$FailedOnly
    records = $records.Count
    total_estimated_tokens = [int]$totalTokens
    total_capture_estimated_tokens = [int]$totalTokens
    total_displayed_estimated_tokens = [int]$totalDisplayedTokens
    displayed_measured_records = $displayMeasuredRecords.Count
    capture_only_records = $captureOnlyRecords.Count
    measurement_coverage_percent = if ($records.Count -eq 0) { 100.0 } else { [math]::Round(($displayMeasuredRecords.Count / [double]$records.Count) * 100.0, 1) }
    blocked_count = @($records | Where-Object { $_.blocked }).Count
    high_or_critical_count = @($records | Where-Object { $_.risk -in @("high", "critical") }).Count
    kind_summary = @($records |
        Group-Object kind |
        ForEach-Object {
            [pscustomobject]@{
                kind = $_.Name
                records = $_.Count
                tokens = [int](($_.Group | Measure-Object -Property tokens -Sum).Sum)
                capture_tokens = [int](($_.Group | Measure-Object -Property capture_tokens -Sum).Sum)
                displayed_tokens = [int](($_.Group | Where-Object { $_.displayed_tokens -ne $null } | Measure-Object -Property displayed_tokens -Sum).Sum)
                measurement_gaps = @($_.Group | Where-Object { $_.measurement_status -ne "measured_display" }).Count
            }
        } |
        Sort-Object capture_tokens -Descending)
    top_commands = @($records |
        Sort-Object @{ Expression = { if ($_.displayed_tokens -eq $null) { -1 } else { [int]$_.displayed_tokens } }; Descending = $true },
                    @{ Expression = "capture_tokens"; Descending = $true } |
        Select-Object -First $Top)
}

if ($Json) {
    $summary | ConvertTo-Json -Depth 6
    exit 0
}

Write-Output ("Token report summary: records={0}, capture_tokens={1}, displayed_tokens={2}, measurement_coverage={3}%, blocked={4}, high_or_critical={5}" -f
    $summary.records,
    $summary.total_capture_estimated_tokens,
    $summary.total_displayed_estimated_tokens,
    $summary.measurement_coverage_percent,
    $summary.blocked_count,
    $summary.high_or_critical_count)
Write-Output ""
Write-Output "Kind summary:"
$summary.kind_summary |
    Select-Object kind, records, capture_tokens, displayed_tokens, measurement_gaps |
    Format-Table -AutoSize
Write-Output ""
$summary.top_commands |
    Select-Object displayed_tokens, capture_tokens, measurement_status, risk, blocked, exit_code, kind, command |
    Format-Table -AutoSize
