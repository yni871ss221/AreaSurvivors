param(
    [ValidateSet("Load200", "Load400", "Load800")]
    [string]$Scenario = "Load800",
    [string]$Path = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$reportDirectory = Join-Path $projectRoot "Library\AreaSafeUnity"
$scenarioName = switch ($Scenario) {
    "Load200" { "Gameplay_Enemy_Load_200" }
    "Load400" { "Gameplay_Enemy_Load_400" }
    "Load800" { "Gameplay_Enemy_Load_800" }
}

if ([string]::IsNullOrWhiteSpace($Path)) {
    $candidate = Get-ChildItem -LiteralPath $reportDirectory `
        -Filter ("combat-performance-matrix-*-{0}.txt" -f $scenarioName) `
        -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $candidate) {
        throw "Performance matrix report was not found for scenario: $scenarioName"
    }
    $resolvedPath = $candidate.FullName
} else {
    $resolvedPath = [System.IO.Path]::GetFullPath(
        (Join-Path (Get-Location).Path $Path))
    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        throw "Performance matrix report does not exist: $resolvedPath"
    }
}

function ConvertTo-Fields {
    param([Parameter(Mandatory = $true)][string]$Line)

    $fields = @{}
    foreach ($segment in ($Line -split ";")) {
        $pair = $segment.Trim() -split "=", 2
        if ($pair.Count -ne 2) { continue }
        $fields[$pair[0].Trim()] = $pair[1].Trim()
    }
    return $fields
}

function ConvertTo-DoubleInvariant {
    param([Parameter(Mandatory = $true)][string]$Value)

    return [double]::Parse(
        $Value,
        [System.Globalization.NumberStyles]::Float,
        [System.Globalization.CultureInfo]::InvariantCulture)
}

$rows = @()
foreach ($line in (Get-Content -LiteralPath $resolvedPath -Encoding UTF8)) {
    if (-not $line.StartsWith("sequence=", [System.StringComparison]::Ordinal)) { continue }
    $fields = ConvertTo-Fields -Line $line
    foreach ($requiredField in @("sequence", "mode", "p95Ms", "avgMs", "maxMs")) {
        if (-not $fields.ContainsKey($requiredField)) {
            throw "Performance matrix row is missing '$requiredField': $line"
        }
    }

    $rows += [pscustomobject]@{
        Sequence = [int]$fields["sequence"]
        Mode = $fields["mode"]
        AverageMs = ConvertTo-DoubleInvariant $fields["avgMs"]
        P95Ms = ConvertTo-DoubleInvariant $fields["p95Ms"]
        MaxMs = ConvertTo-DoubleInvariant $fields["maxMs"]
    }
}

if ($rows.Count -lt 2) {
    throw "Performance matrix report must contain at least two rows: $resolvedPath"
}

$baselines = @($rows | Where-Object { $_.Mode -eq "Baseline" } | Sort-Object Sequence)
if ($baselines.Count -lt 2) {
    throw "Performance matrix report must contain opening and closing Baseline rows: $resolvedPath"
}

$openingBaseline = $baselines[0]
$closingBaseline = $baselines[$baselines.Count - 1]
$referenceP95 = ($openingBaseline.P95Ms + $closingBaseline.P95Ms) / 2.0
$driftPercent = if ($openingBaseline.P95Ms -gt 0) {
    (($closingBaseline.P95Ms - $openingBaseline.P95Ms) / $openingBaseline.P95Ms) * 100.0
} else {
    0.0
}
$driftStatus = if ([Math]::Abs($driftPercent) -le 10.0) {
    "stable"
} elseif ([Math]::Abs($driftPercent) -le 20.0) {
    "caution"
} else {
    "high-drift"
}

$ranked = @(
    $rows |
        Where-Object { $_.Mode -ne "Baseline" } |
        ForEach-Object {
            [pscustomobject]@{
                Mode = $_.Mode
                P95Ms = [Math]::Round($_.P95Ms, 2)
                ImprovementPercent = if ($referenceP95 -gt 0) {
                    [Math]::Round((($referenceP95 - $_.P95Ms) / $referenceP95) * 100.0, 1)
                } else {
                    0.0
                }
                AverageMs = [Math]::Round($_.AverageMs, 2)
                MaxMs = [Math]::Round($_.MaxMs, 2)
            }
        } |
        Sort-Object ImprovementPercent -Descending
)

Write-Output ("matrix_report_path: {0}" -f $resolvedPath)
Write-Output ("scenario: {0}" -f $scenarioName)
Write-Output ("opening_baseline_p95_ms: {0:0.00}" -f $openingBaseline.P95Ms)
Write-Output ("closing_baseline_p95_ms: {0:0.00}" -f $closingBaseline.P95Ms)
Write-Output ("baseline_drift_percent: {0:+0.0;-0.0;0.0}" -f $driftPercent)
Write-Output ("baseline_drift_status: {0}" -f $driftStatus)
Write-Output ("comparison_reference_p95_ms: {0:0.00}" -f $referenceP95)
Write-Output "mode_ranking:"
$ranked |
    Format-Table Mode, P95Ms, ImprovementPercent, AverageMs, MaxMs -AutoSize |
    Out-String -Width 160 |
    Write-Output
