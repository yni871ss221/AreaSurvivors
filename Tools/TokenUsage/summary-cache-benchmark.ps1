[CmdletBinding()]
param(
    [string]$ReportPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$cacheRoot = Join-Path $projectRoot `
    "Library\AreaAgentIndex\SemanticSummaries\Entries"
$cacheScript = Join-Path $PSScriptRoot "semantic-summary-cache.ps1"
$commonPath = Join-Path $PSScriptRoot "TokenUsageCommon.ps1"
. $commonPath

$results = @(
    if (Test-Path -LiteralPath $cacheRoot -PathType Container) {
        foreach ($entryFile in @(
                Get-ChildItem -LiteralPath $cacheRoot -Filter "*.json" -File
            )) {
            try {
                $entry = Get-Content -LiteralPath $entryFile.FullName `
                    -Raw `
                    -Encoding UTF8 |
                    ConvertFrom-Json
                $relativePath = [string]$entry.path
                $absolutePath = [System.IO.Path]::GetFullPath(
                    (Join-Path $projectRoot $relativePath)
                )
                $projectPrefix =
                    $projectRoot.TrimEnd("\", "/") +
                    [System.IO.Path]::DirectorySeparatorChar
                if (-not $absolutePath.StartsWith(
                        $projectPrefix,
                        [System.StringComparison]::OrdinalIgnoreCase
                    ) -or
                    -not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
                    continue
                }

                $summaryLines = @(
                    & $cacheScript `
                        -Action Query `
                        -Path $relativePath `
                        -NoUsageTracking
                )
                $summaryText = $summaryLines -join "`n"
                if ($summaryText -notmatch
                    "(?m)^semantic_summary_cache: hit;") {
                    continue
                }

                $sourceText = [System.IO.File]::ReadAllText($absolutePath)
                $firstLines = @(
                    [System.IO.File]::ReadAllLines($absolutePath) |
                        Select-Object -First 50
                ) -join "`n"
                $sourceEstimate = Get-TokenUsageEstimate `
                    -Source $relativePath `
                    -Text $sourceText
                $boundedEstimate = Get-TokenUsageEstimate `
                    -Source ($relativePath + ":first50") `
                    -Text $firstLines
                $summaryEstimate = Get-TokenUsageEstimate `
                    -Source ($relativePath + ":semantic-summary") `
                    -Text $summaryText

                [pscustomobject][ordered]@{
                    path = $relativePath
                    content_sha256 = [string]$entry.content_sha256
                    full_source_estimated_tokens =
                        [int]$sourceEstimate.estimated_tokens
                    first_50_lines_estimated_tokens =
                        [int]$boundedEstimate.estimated_tokens
                    summary_estimated_tokens =
                        [int]$summaryEstimate.estimated_tokens
                }
            } catch {
                continue
            }
        }
    }
)

$fullTokens = [int](($results |
            Measure-Object -Property full_source_estimated_tokens -Sum).Sum)
$boundedTokens = [int](($results |
            Measure-Object -Property first_50_lines_estimated_tokens -Sum).Sum)
$summaryTokens = [int](($results |
            Measure-Object -Property summary_estimated_tokens -Sum).Sum)
function Get-ReductionPercent {
    param([int]$Baseline, [int]$Current)

    if ($Baseline -le 0) {
        return 0
    }
    return [math]::Round(100.0 * ($Baseline - $Current) / $Baseline, 1)
}

$report = [pscustomobject][ordered]@{
    schema_version = 1
    benchmark = "semantic_summary_cache"
    generated_at = [DateTime]::UtcNow.ToString("o")
    entry_count = $results.Count
    full_source_reference = [pscustomobject][ordered]@{
        baseline_estimated_tokens = $fullTokens
        summary_estimated_tokens = $summaryTokens
        reduction_percent =
            Get-ReductionPercent -Baseline $fullTokens -Current $summaryTokens
    }
    first_50_lines_reference = [pscustomobject][ordered]@{
        baseline_estimated_tokens = $boundedTokens
        summary_estimated_tokens = $summaryTokens
        reduction_percent =
            Get-ReductionPercent -Baseline $boundedTokens -Current $summaryTokens
    }
    coverage_note = "Static display comparison only. Full-source is an upper bound; first-50-lines is a fixed bounded reference. Neither represents total model tokens or proves semantic equivalence."
    entries = $results
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $projectRoot `
        "TokenReports\Benchmarks\semantic-summary-cache-latest.json"
} elseif (-not [System.IO.Path]::IsPathRooted($ReportPath)) {
    $ReportPath = Join-Path $projectRoot $ReportPath
}
$reportDirectory = Split-Path $ReportPath -Parent
if (-not (Test-Path -LiteralPath $reportDirectory)) {
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
}
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText(
    $ReportPath,
    ($report | ConvertTo-Json -Depth 8),
    $utf8NoBom
)

Write-Output "summary_cache_benchmark_entries: $($results.Count)"
Write-Output (
    "summary_cache_benchmark: reference=full_source; baseline={0}; summary={1}; reduction={2}%" -f
    $fullTokens,
    $summaryTokens,
    $report.full_source_reference.reduction_percent
)
Write-Output (
    "summary_cache_benchmark: reference=first_50_lines; baseline={0}; summary={1}; reduction={2}%" -f
    $boundedTokens,
    $summaryTokens,
    $report.first_50_lines_reference.reduction_percent
)
Write-Output "report_path: $ReportPath"
Write-Output "coverage_note: static display comparison; full source is an upper bound and first 50 lines is a fixed bounded reference"
