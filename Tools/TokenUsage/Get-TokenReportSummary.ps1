param(
    [string]$ReportDirectory = "TokenReports",
    [int]$Days = 7,
    [string]$Since = "",
    [string[]]$Kind = @(),
    [int]$Top = 10,
    [int]$Recent = 0,
    [switch]$SinceLastStart,
    [switch]$IncludeBenchmark,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ReportDirectory)) {
    Write-Output "No TokenReports directory found."
    exit 0
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
$records = @()
foreach ($file in Get-ChildItem -LiteralPath $ReportDirectory -Filter "*.jsonl" -File | Where-Object { $_.LastWriteTime -ge $sinceDate }) {
    foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding UTF8) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try {
            $record = $line | ConvertFrom-Json
            $tokens = 0
            if ($record.estimate -and $record.estimate.estimated_tokens -ne $null) {
                $tokens = [int]$record.estimate.estimated_tokens
            }
            $records += [pscustomobject]@{
                timestamp = $record.timestamp
                kind = $record.kind
                command = $record.command
                tokens = $tokens
                risk = if ($record.estimate) { $record.estimate.risk } else { "" }
                blocked = [bool]$record.blocked
                exit_code = $record.exit_code
                report_file = $file.Name
            }
        } catch {
            $records += [pscustomobject]@{
                timestamp = ""
                kind = "parse_error"
                command = $file.Name
                tokens = 0
                risk = "unknown"
                blocked = $false
                exit_code = $null
                report_file = $file.Name
            }
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
if ($Recent -gt 0) {
    $records = @($records | Sort-Object timestamp | Select-Object -Last $Recent)
}

$totalTokens = ($records | Measure-Object -Property tokens -Sum).Sum
$summary = [pscustomobject]@{
    report_directory = (Resolve-Path -LiteralPath $ReportDirectory).Path
    days = $Days
    since = $sinceDate.ToString("o")
    kinds = @($kindSet.Keys)
    recent = $Recent
    since_last_start = [bool]$SinceLastStart
    records = $records.Count
    total_estimated_tokens = [int]$totalTokens
    blocked_count = @($records | Where-Object { $_.blocked }).Count
    high_or_critical_count = @($records | Where-Object { $_.risk -in @("high", "critical") }).Count
    top_commands = @($records | Sort-Object tokens -Descending | Select-Object -First $Top)
}

if ($Json) {
    $summary | ConvertTo-Json -Depth 6
    exit 0
}

Write-Output ("Token report summary: records={0}, total_estimated_tokens={1}, blocked={2}, high_or_critical={3}" -f $summary.records, $summary.total_estimated_tokens, $summary.blocked_count, $summary.high_or_critical_count)
Write-Output ""
$summary.top_commands |
    Select-Object tokens, risk, blocked, exit_code, kind, command |
    Format-Table -AutoSize
