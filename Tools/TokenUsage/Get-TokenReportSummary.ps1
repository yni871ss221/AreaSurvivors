param(
    [string]$ReportDirectory = "TokenReports",
    [int]$Days = 7,
    [int]$Top = 10,
    [switch]$IncludeBenchmark,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ReportDirectory)) {
    Write-Output "No TokenReports directory found."
    exit 0
}

$since = (Get-Date).Date.AddDays(-[math]::Max(0, $Days - 1))
$records = @()
foreach ($file in Get-ChildItem -LiteralPath $ReportDirectory -Filter "*.jsonl" -File | Where-Object { $_.LastWriteTime -ge $since }) {
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

if (-not $IncludeBenchmark) {
    $records = @($records | Where-Object { $_.kind -ne "benchmark" })
}

$totalTokens = ($records | Measure-Object -Property tokens -Sum).Sum
$summary = [pscustomobject]@{
    report_directory = (Resolve-Path -LiteralPath $ReportDirectory).Path
    days = $Days
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
