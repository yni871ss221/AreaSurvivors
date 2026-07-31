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
    [switch]$SelfTest,
    [switch]$Json,
    [switch]$ForceRebuild
)

$ErrorActionPreference = "Stop"

$indexScript = Join-Path $PSScriptRoot "token-report-index.py"
if (-not (Test-Path -LiteralPath $indexScript -PathType Leaf)) {
    throw "Token report index implementation was not found: $indexScript"
}

$pythonCommand = Get-Command "python" -ErrorAction SilentlyContinue
if ($null -eq $pythonCommand) {
    throw "Python is required for the TokenReports incremental SQLite index."
}

$arguments = @(
    $indexScript,
    "--report-directory", $ReportDirectory,
    "--days", $Days.ToString([Globalization.CultureInfo]::InvariantCulture),
    "--top", $Top.ToString([Globalization.CultureInfo]::InvariantCulture),
    "--recent", $Recent.ToString([Globalization.CultureInfo]::InvariantCulture)
)
if (-not [string]::IsNullOrWhiteSpace($Since)) {
    $arguments += @("--since", $Since)
}
foreach ($kindEntry in $Kind) {
    if (-not [string]::IsNullOrWhiteSpace($kindEntry)) {
        $arguments += @("--kind", $kindEntry)
    }
}
if ($SinceLastStart) { $arguments += "--since-last-start" }
if ($FailedOnly) { $arguments += "--failed-only" }
if ($IncludeBenchmark) { $arguments += "--include-benchmark" }
if ($ForceRebuild) { $arguments += "--force-rebuild" }
if ($SelfTest) { $arguments += "--self-test" }

$outputJsonPath = ""
if (-not $SelfTest) {
    $outputJsonPath = Join-Path (
        [System.IO.Path]::GetTempPath()
    ) ("area-token-summary-" + [guid]::NewGuid().ToString("N") + ".json")
    $arguments += @("--output-json", $outputJsonPath)
}

try {
    $output = @(& $pythonCommand.Source @arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $consoleText = $output -join "`n"
    if ($exitCode -ne 0) {
        throw "Token report index failed with exit code ${exitCode}: $consoleText"
    }
    if ($SelfTest) {
        Write-Output $consoleText
        exit 0
    }
    if (-not (Test-Path -LiteralPath $outputJsonPath -PathType Leaf)) {
        throw "Token report index did not create its UTF-8 result file."
    }
    $outputText = [System.IO.File]::ReadAllText(
        $outputJsonPath,
        [System.Text.Encoding]::UTF8
    )
    $summary = $outputText | ConvertFrom-Json
} finally {
    if (-not [string]::IsNullOrWhiteSpace($outputJsonPath) -and
        (Test-Path -LiteralPath $outputJsonPath)) {
        Remove-Item -LiteralPath $outputJsonPath -Force
    }
}

if ($null -ne $summary.PSObject.Properties["message"]) {
    Write-Output $summary.message
    exit 0
}
if ($Json) {
    Write-Output $outputText
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
if (@($summary.operation_summary).Count -gt 0) {
    Write-Output ""
    Write-Output "Operation summary:"
    $summary.operation_summary |
        Select-Object operation, records, capture_tokens, displayed_tokens |
        Format-Table -AutoSize
}
