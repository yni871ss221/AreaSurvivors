$ErrorActionPreference = "Stop"

$toolsRoot = Split-Path $PSScriptRoot -Parent
$cachePath = Join-Path $toolsRoot "semantic-summary-cache.ps1"
$overviewPath = Join-Path $toolsRoot "code-file-overview.ps1"
$benchmarkPath = Join-Path $toolsRoot "summary-cache-benchmark.ps1"

foreach ($requiredPath in @($cachePath, $overviewPath, $benchmarkPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Semantic summary tool is missing: $requiredPath"
    }
}

$cacheCommand = Get-Command $cachePath
foreach ($parameterName in @(
        "Action",
        "Path",
        "ExpectedHash",
        "Purpose",
        "Invariants",
        "SideEffects",
        "Verification"
    )) {
    if (-not $cacheCommand.Parameters.ContainsKey($parameterName)) {
        throw "Semantic summary cache parameter is missing: $parameterName"
    }
}

$selfTestOutput = @(& $cachePath -Action SelfTest) -join "`n"
if ($selfTestOutput -notmatch "semantic_summary_cache_self_test: passed") {
    throw "Semantic summary cache self-test did not complete."
}

$scopeRejected = $false
try {
    & $cachePath -Action Query -Path "AGENTS.md" | Out-Null
} catch {
    $scopeRejected = $_.Exception.Message -match
        "limited to AreaSurvivors C# and Tools PowerShell"
}
if (-not $scopeRejected) {
    throw "Semantic summary cache accepted an authoritative non-source file."
}

$escapeRejected = $false
try {
    & $cachePath -Action Query -Path "..\outside.ps1" | Out-Null
} catch {
    $escapeRejected = $_.Exception.Message -match "escaped the project root"
}
if (-not $escapeRejected) {
    throw "Semantic summary cache accepted a path outside the project."
}

$overviewText = [System.IO.File]::ReadAllText($overviewPath)
if (-not $overviewText.Contains("structure-index.ps1") -or
    -not $overviewText.Contains("semantic-summary-cache.ps1")) {
    throw "Code.File overview must compose structure and semantic summary data."
}

Write-Output "command_tool_test_module: semantic-summary-cache passed"
