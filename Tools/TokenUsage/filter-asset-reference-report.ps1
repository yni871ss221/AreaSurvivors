param(
    [string]$ReportPath = "",
    [string]$Status = "review-candidate",
    [int]$Top = 20,
    [string]$ExportPath = "",
    [ValidateSet("table", "tsv", "md")]
    [string]$ExportFormat = "md",
    [switch]$Json
)

$ErrorActionPreference = "Stop"

function Get-LatestReportPath {
    $latest = Get-ChildItem -LiteralPath "TokenReports/UnityReports" -Filter "asset-references-*.md" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $latest) {
        throw "No asset reference report found in TokenReports/UnityReports."
    }
    return $latest.FullName
}

function Parse-AssetReferenceReport {
    param([string[]]$Lines)

    $items = New-Object System.Collections.Generic.List[object]
    $current = $null

    foreach ($line in $Lines) {
        if ($line -match '^\[Asset\]\s+(.+)$') {
            if ($null -ne $current) { $items.Add([pscustomobject]$current) }
            $current = [ordered]@{
                Path = $Matches[1].Trim()
                Type = ""
                KB = 0.0
                Guid = ""
                InGeneratedCatalog = ""
                GeneratedNameKnown = ""
                ScenePrefabAssetGuidRefs = 0
                CodeNameRefs = 0
                Status = ""
            }
            continue
        }

        if ($null -eq $current) { continue }

        if ($line -match '^- type:\s+(\S+)\s+kb=([0-9.]+)$') {
            $current.Type = $Matches[1]
            $current.KB = [double]$Matches[2]
            continue
        }
        if ($line -match '^- guid:\s+(.+)$') {
            $current.Guid = $Matches[1].Trim()
            continue
        }
        if ($line -match '^- inGeneratedCatalog:\s+(.+)$') {
            $current.InGeneratedCatalog = $Matches[1].Trim()
            continue
        }
        if ($line -match '^- generatedNameKnown:\s+(.+)$') {
            $current.GeneratedNameKnown = $Matches[1].Trim()
            continue
        }
        if ($line -match '^- scenePrefabAssetGuidRefs:\s+([0-9]+)$') {
            $current.ScenePrefabAssetGuidRefs = [int]$Matches[1]
            continue
        }
        if ($line -match '^- codeNameRefs:\s+([0-9]+)$') {
            $current.CodeNameRefs = [int]$Matches[1]
            continue
        }
        if ($line -match '^- status:\s+(.+)$') {
            $current.Status = $Matches[1].Trim()
            continue
        }
    }

    if ($null -ne $current) { $items.Add([pscustomobject]$current) }
    return @($items.ToArray())
}

function Convert-ToDecisionRows {
    param([object[]]$Items)

    return @(
        foreach ($item in $Items) {
            [pscustomobject]@{
                Decision = ""
                Reason = ""
                Path = $item.Path
                KB = $item.KB
                Type = $item.Type
                ScenePrefabAssetGuidRefs = $item.ScenePrefabAssetGuidRefs
                CodeNameRefs = $item.CodeNameRefs
                Status = $item.Status
            }
        }
    )
}

function Write-DecisionExport {
    param(
        [string]$Path,
        [string]$Format,
        [object[]]$Rows,
        [string]$SourceReportPath,
        [string]$FilterStatus
    )

    if (-not [System.IO.Path]::IsPathRooted($Path)) {
        $Path = Join-Path (Get-Location).Path $Path
    }

    $dir = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    switch ($Format) {
        "tsv" {
            $lines = @("Decision`tReason`tPath`tKB`tType`tScenePrefabAssetGuidRefs`tCodeNameRefs`tStatus")
            foreach ($row in $Rows) {
                $lines += "{0}`t{1}`t{2}`t{3}`t{4}`t{5}`t{6}`t{7}" -f `
                    $row.Decision, $row.Reason, $row.Path, $row.KB, $row.Type, $row.ScenePrefabAssetGuidRefs, $row.CodeNameRefs, $row.Status
            }
            Set-Content -LiteralPath $Path -Value $lines -Encoding utf8
        }
        "md" {
            $lines = @(
                "# Asset Reference Review"
                ""
                "- Source report: $SourceReportPath"
                "- Filter status: $FilterStatus"
                "- Rows: $($Rows.Count)"
                ""
                "| Decision | Reason | Path | KB | Type | GuidRefs | CodeRefs | Status |"
                "| --- | --- | --- | ---: | --- | ---: | ---: | --- |"
            )
            foreach ($row in $Rows) {
                $lines += "| $($row.Decision) | $($row.Reason) | $($row.Path) | $($row.KB) | $($row.Type) | $($row.ScenePrefabAssetGuidRefs) | $($row.CodeNameRefs) | $($row.Status) |"
            }
            Set-Content -LiteralPath $Path -Value $lines -Encoding utf8
        }
    }

    return $Path
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Get-LatestReportPath
}

$lines = Get-Content -LiteralPath $ReportPath -Encoding utf8
$items = Parse-AssetReferenceReport -Lines $lines
$filtered = @(
    $items |
        Where-Object { $_.Status -eq $Status } |
        Sort-Object @{ Expression = "KB"; Descending = $true }, @{ Expression = "Path"; Descending = $false } |
        Select-Object -First $Top
)
$decisionRows = Convert-ToDecisionRows -Items $filtered

if (-not [string]::IsNullOrWhiteSpace($ExportPath)) {
    $ExportPath = Write-DecisionExport -Path $ExportPath -Format $ExportFormat -Rows $decisionRows -SourceReportPath $ReportPath -FilterStatus $Status
}

$result = [pscustomobject]@{
    report_path = $ReportPath
    status = $Status
    total_assets = @($items).Count
    matched_assets = @($filtered).Count
    export_path = $ExportPath
    export_format = $ExportFormat
    assets = $filtered
}

if ($Json) {
    $result | ConvertTo-Json -Depth 5
    exit 0
}

Write-Output "Asset reference report filter"
Write-Output "Report: $ReportPath"
Write-Output "Status: $Status"
Write-Output "Matched: $(@($filtered).Count) / $(@($items).Count)"
if (-not [string]::IsNullOrWhiteSpace($ExportPath)) {
    Write-Output "Export: $ExportPath ($ExportFormat)"
}
Write-Output ""
$filtered | Format-Table -AutoSize Path,KB,Type,ScenePrefabAssetGuidRefs,CodeNameRefs,Status
