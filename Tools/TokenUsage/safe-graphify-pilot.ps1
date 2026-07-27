param(
    [ValidateSet("Build", "Update", "EnsureFresh", "Query", "Path", "Explain", "Affected", "Diagnose", "Benchmark", "Status")]
    [string]$Action = "Status",
    [string]$Question = "",
    [string]$Source = "",
    [string]$Target = "",
    [ValidateRange(200, 1200)]
    [int]$Budget = 800,
    [ValidateRange(1, 6)]
    [int]$Depth = 2,
    [string[]]$Context = @(),
    [ValidateRange(1, 8)]
    [int]$MaxWorkers = 4,
    [ValidateRange(0.5, 1.0)]
    [double]$MinimumRetainedRatio = 0.8,
    [ValidateSet("production", "evaluation")]
    [string]$UsageCategory = "production",
    [ValidateRange(1, 100)]
    [int]$AffectedDisplayLimit = 20,
    [ValidateRange(100, 5000)]
    [int]$AffectedTokenLimit = 500,
    [switch]$AllowStale,
    [switch]$AllowGraphShrink,
    [switch]$ShowFullAffected,
    [switch]$PrintOutput
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$graphPath = Join-Path $projectRoot "graphify-out\graph.json"
$inspectScript = Join-Path $PSScriptRoot "graphify-pilot-inspect.py"
$userProfilePath = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
$defaultPythonPath = Join-Path $userProfilePath ".cache\AreaSurvivors\graphify-pilot-0.9.26\Scripts\python.exe"
$pythonPath = if ([string]::IsNullOrWhiteSpace($env:AREA_SURVIVORS_GRAPHIFY_PYTHON)) {
    $defaultPythonPath
} else {
    $env:AREA_SURVIVORS_GRAPHIFY_PYTHON
}
$reportDirectory = Join-Path $projectRoot "TokenReports"
$usageLogPath = Join-Path $reportDirectory "graphify-pilot-usage.jsonl"
$graphifyTarget = "."

if (-not (Test-Path -LiteralPath $pythonPath -PathType Leaf)) {
    throw "Graphify Pilot Python is missing: $pythonPath"
}
if (-not (Test-Path -LiteralPath $inspectScript -PathType Leaf)) {
    throw "Graphify Pilot inspector is missing: $inspectScript"
}
if (-not (Test-Path -LiteralPath $reportDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $reportDirectory | Out-Null
}
Set-Location -LiteralPath $projectRoot

function Get-GraphifyOutputSignals {
    param(
        [Parameter(Mandatory = $true)][string]$ActionName,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [AllowEmptyCollection()]
        [string[]]$OutputLines
    )

    $outputText = $OutputLines -join [Environment]::NewLine
    $reasons = @()
    if ($outputText -match '(?i)\bambiguous\b') {
        $reasons += "ambiguous"
    }
    if ($outputText.Contains("[INFERRED]")) {
        $reasons += "inferred-edge"
    }
    if ($outputText -match '(?i)(?:\.\.\.\s*and\s+\d+\s+more|truncat)') {
        $reasons += "truncated"
    }
    if ($ActionName -in @("Explain", "Path", "Affected") -and
        $outputText -match '(?m)^\s*Source:\s*$') {
        $reasons += "missing-source-path"
    }

    $resultCount = $null
    if ($ActionName -eq "Affected") {
        $resultCount = @($OutputLines | Where-Object { $_ -match '^\s*-\s+' }).Count
        if ($resultCount -le 1) {
            $reasons += "affected-result-count-$resultCount"
        }
    }
    if ($ActionName -eq "Path" -and $outputText -match '(?i)(?:no path|not found|no match)') {
        $reasons += "path-not-found"
    }

    $estimatedTokens = [int][Math]::Ceiling($outputText.Length / 4.0)
    if ($ActionName -eq "Affected") {
        if ($resultCount -gt $AffectedDisplayLimit) {
            $reasons += "affected-result-count-high-$resultCount"
        }
        if ($estimatedTokens -gt $AffectedTokenLimit) {
            $reasons += "affected-output-tokens-high-$estimatedTokens"
        }
    }

    return [pscustomobject]@{
        OutputText = $outputText
        EstimatedTokens = $estimatedTokens
        ResultCount = $resultCount
        Reasons = @($reasons)
        VerificationRequired = ($reasons.Count -gt 0)
    }
}

function ConvertTo-SingleQuotedArgument {
    param([Parameter(Mandatory = $true)][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function Write-GraphifyFallbackRecommendation {
    param(
        [Parameter(Mandatory = $true)][object]$Signals,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$OutputText,
        [AllowEmptyString()]
        [string]$FallbackId = ""
    )

    if (-not $Signals.VerificationRequired) {
        return
    }

    $fallbackPath = if ($OutputText -match '(?i)Tools[/\\].+\.ps1') {
        "Tools"
    } else {
        "Assets/AreaSurvivors"
    }
    $fallbackExtension = if ($fallbackPath -eq "Tools") { "ps1" } else { "cs" }
    $patterns = @()
    if (-not [string]::IsNullOrWhiteSpace($Source)) {
        $patterns += $Source
    }
    if ($Action -eq "Path" -and -not [string]::IsNullOrWhiteSpace($Target)) {
        $patterns += $Target
    }

    Write-Output "graphify_verification_required: true"
    Write-Output ("verification_reasons: {0}" -f ($Signals.Reasons -join ","))
    if ($Signals.Reasons -match '^affected-(?:result-count|output-tokens)-high-') {
        Write-Output "graphify_refine_recommendation: rerun Affected with -Depth 1 or use Path with a known target"
    }
    foreach ($pattern in $patterns) {
        $quotedPattern = ConvertTo-SingleQuotedArgument -Value $pattern
        $quotedPath = ConvertTo-SingleQuotedArgument -Value $fallbackPath
        $quotedFallbackId = ConvertTo-SingleQuotedArgument -Value $FallbackId
        Write-Output (
            "fallback_command: rtk powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/TokenUsage/focused-search.ps1 -Pattern {0} -Path {1} -TopFiles 3 -Context 3 -MaxMatchesPerFile 1 -Extension {2} -GraphifyFallbackId {3} -GraphifyUsageCategory {4} -PrintOutput" -f
                $quotedPattern, $quotedPath, $fallbackExtension, $quotedFallbackId, $UsageCategory
        )
    }
}

function Write-GraphifyUsageRecord {
    param(
        [Parameter(Mandatory = $true)][string]$RecordedAction,
        [Parameter(Mandatory = $true)][int]$ElapsedMilliseconds,
        [Parameter(Mandatory = $true)][int]$ExitCode,
        [Parameter(Mandatory = $true)][object]$Signals,
        [bool]$RebuildPerformed = $false,
        [AllowEmptyString()]
        [string]$FallbackId = "",
        [bool]$OutputLimited = $false,
        [int]$DisplayedResultCount = 0,
        [AllowEmptyString()]
        [string]$CapturePath = ""
    )

    $record = [ordered]@{
        timestamp = (Get-Date).ToString("o")
        graphify_version = "0.9.26"
        usage_category = $UsageCategory
        action = $RecordedAction
        source = $Source
        target = $Target
        elapsed_ms = $ElapsedMilliseconds
        exit_code = $ExitCode
        estimated_output_tokens = $Signals.EstimatedTokens
        result_count = $Signals.ResultCount
        verification_required = $Signals.VerificationRequired
        verification_reasons = @($Signals.Reasons)
        rebuild_performed = $RebuildPerformed
        fallback_recommended = (-not [string]::IsNullOrWhiteSpace($FallbackId))
        fallback_id = $FallbackId
        output_limited = $OutputLimited
        displayed_result_count = $DisplayedResultCount
        full_capture_path = $CapturePath
    }
    $jsonLine = $record | ConvertTo-Json -Compress -Depth 4
    [System.IO.File]::AppendAllText(
        $usageLogPath,
        $jsonLine + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false)
    )
}

function Invoke-GraphifyCommand {
    param(
        [Parameter(Mandatory = $true)][string[]]$ArgumentList,
        [Parameter(Mandatory = $true)][string]$Label,
        [switch]$ShowAll,
        [switch]$TrackUsage
    )

    $startedAt = Get-Date
    $nativeErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $commandOutput = @(& $pythonPath -m graphify @ArgumentList 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $nativeErrorActionPreference
    }
    $commandOutput = @($commandOutput | ForEach-Object { [string]$_ })
    $elapsedMilliseconds = [int]((Get-Date) - $startedAt).TotalMilliseconds
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss-fff"
    $capturePath = Join-Path $reportDirectory ("graphify-pilot-{0}-{1}.log" -f $timestamp, $Label)
    [System.IO.File]::WriteAllLines($capturePath, [string[]]$commandOutput, [System.Text.UTF8Encoding]::new($false))
    $signals = Get-GraphifyOutputSignals -ActionName $Action -OutputLines $commandOutput
    $fallbackId = if ($signals.VerificationRequired) { [guid]::NewGuid().ToString("N") } else { "" }
    $outputLimited = (
        $Action -eq "Affected" -and
        -not $ShowFullAffected -and
        ($signals.ResultCount -gt $AffectedDisplayLimit -or $signals.EstimatedTokens -gt $AffectedTokenLimit)
    )
    $displayedResultCount = if ($outputLimited) {
        [Math]::Min($AffectedDisplayLimit, [int]$signals.ResultCount)
    } elseif ($null -eq $signals.ResultCount) {
        0
    } else {
        [int]$signals.ResultCount
    }
    if ($TrackUsage) {
        Write-GraphifyUsageRecord -RecordedAction $Action -ElapsedMilliseconds $elapsedMilliseconds -ExitCode $exitCode -Signals $signals -FallbackId $fallbackId -OutputLimited $outputLimited -DisplayedResultCount $displayedResultCount -CapturePath $capturePath
    }

    if ($exitCode -ne 0) {
        Write-Output ("graphify_pilot_action: {0}" -f $Label)
        Write-Output ("exit_code: {0}" -f $exitCode)
        Write-Output ("elapsed_ms: {0}" -f $elapsedMilliseconds)
        Write-Output ("capture_path: {0}" -f $capturePath)
        $commandOutput | Select-Object -Last 20
        exit $exitCode
    }

    if ($outputLimited) {
        @($commandOutput | Where-Object { $_ -notmatch '^\s*-\s+' } | Select-Object -First 12)
        @($commandOutput | Where-Object { $_ -match '^\s*-\s+' } | Select-Object -First $AffectedDisplayLimit)
        Write-Output "affected_output_limited: true"
        Write-Output ("shown_results: {0}" -f $displayedResultCount)
        Write-Output ("total_results: {0}" -f $signals.ResultCount)
        Write-Output ("estimated_full_output_tokens: {0}" -f $signals.EstimatedTokens)
        Write-Output ("full_capture_path: {0}" -f $capturePath)
    } elseif ($ShowAll -or $PrintOutput) {
        $commandOutput
    } else {
        $commandOutput | Select-Object -Last 12
    }
    if ($TrackUsage) {
        Write-GraphifyFallbackRecommendation -Signals $signals -OutputText $signals.OutputText -FallbackId $fallbackId
    }
    Write-Output ("graphify_pilot_action: {0}" -f $Label)
    Write-Output ("exit_code: 0")
    Write-Output ("elapsed_ms: {0}" -f $elapsedMilliseconds)
    Write-Output ("capture_path: {0}" -f $capturePath)
}

function Assert-ClusteredGraph {
    param([switch]$ShowInspection)

    if (-not (Test-Path -LiteralPath $graphPath -PathType Leaf)) {
        throw "Graphify graph is missing. Run -Action Build first: $graphPath"
    }
    $inspectionOutput = @(& $pythonPath $inspectScript --graph $graphPath --root $projectRoot 2>&1)
    $inspectionExitCode = $LASTEXITCODE
    if ($ShowInspection) {
        $inspectionOutput
    }
    if ($inspectionExitCode -ne 0) {
        throw "Graphify graph schema/integrity check failed with exit code $inspectionExitCode. Build must finish with cluster-only --no-label --no-viz."
    }
}

function Get-RawGraphInspection {
    if (-not (Test-Path -LiteralPath $graphPath -PathType Leaf)) {
        throw "Graphify raw graph is missing after extraction: $graphPath"
    }
    $inspectionOutput = @(& $pythonPath $inspectScript --graph $graphPath --root $projectRoot --allow-raw 2>&1)
    $inspectionExitCode = $LASTEXITCODE
    $inspectionOutput
    if ($inspectionExitCode -ne 0) {
        throw "Graphify raw graph integrity check failed with exit code $inspectionExitCode."
    }
    return ($inspectionOutput[-1] | ConvertFrom-Json)
}

function Get-GraphFreshness {
    $sourceRoots = @(
        (Join-Path $projectRoot "Assets\AreaSurvivors"),
        (Join-Path $projectRoot "Tools")
    )
    $latestSource = $null
    foreach ($sourceRoot in $sourceRoots) {
        if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
            continue
        }
        $candidate = Get-ChildItem -LiteralPath $sourceRoot -Recurse -File |
            Where-Object { $_.Extension -eq ".cs" -or $_.Extension -eq ".ps1" -or $_.Extension -eq ".py" } |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        if ($null -ne $candidate -and ($null -eq $latestSource -or $candidate.LastWriteTimeUtc -gt $latestSource.LastWriteTimeUtc)) {
            $latestSource = $candidate
        }
    }

    $graphExists = Test-Path -LiteralPath $graphPath -PathType Leaf
    $graphWriteTime = if ($graphExists) {
        (Get-Item -LiteralPath $graphPath).LastWriteTimeUtc
    } else {
        [DateTime]::MinValue
    }
    $isFresh = $graphExists -and ($null -eq $latestSource -or $latestSource.LastWriteTimeUtc -le $graphWriteTime)
    return [pscustomobject]@{
        GraphExists = $graphExists
        IsFresh = $isFresh
        GraphWriteTimeUtc = $graphWriteTime
        LatestSource = $latestSource
    }
}

function Assert-GraphFreshness {
    if ($AllowStale) {
        return
    }

    $freshness = Get-GraphFreshness
    if (-not $freshness.IsFresh) {
        $latestSourcePath = if ($null -ne $freshness.LatestSource) {
            $freshness.LatestSource.FullName
        } else {
            "(graph missing)"
        }
        throw ("Graphify graph is stale (guard_code: 61). Latest source: {0}. Run -Action EnsureFresh before querying." -f $latestSourcePath)
    }
}

function Invoke-FullGraphRefresh {
    param(
        [Parameter(Mandatory = $true)][string]$ExtractLabel,
        [Nullable[int]]$BeforeNodeCount
    )

    Invoke-GraphifyCommand -Label $ExtractLabel -ArgumentList @(
        "extract", $graphifyTarget, "--code-only", "--no-cluster", "--max-workers", [string]$MaxWorkers, "--force"
    )
    $rawInspection = Get-RawGraphInspection
    if ($null -eq $BeforeNodeCount) {
        if (-not $AllowGraphShrink -and [int]$rawInspection.nodes -lt 1000) {
            throw ("Graphify full build produced too few nodes (guard_code: 62): {0}" -f $rawInspection.nodes)
        }
    } else {
        $minimumNodes = [Math]::Floor([int]$BeforeNodeCount * $MinimumRetainedRatio)
        if (-not $AllowGraphShrink -and [int]$rawInspection.nodes -lt $minimumNodes) {
            throw ("Graphify update shrank below the retained-node guard (guard_code: 63): before={0}, after={1}, minimum={2}" -f $BeforeNodeCount, $rawInspection.nodes, $minimumNodes)
        }
    }
    Invoke-GraphifyCommand -Label "cluster" -ArgumentList @(
        "cluster-only", $graphifyTarget, "--no-viz", "--no-label"
    )
    Assert-ClusteredGraph
}

if ($Action -eq "Build") {
    Invoke-FullGraphRefresh -ExtractLabel "extract"
    Assert-ClusteredGraph -ShowInspection
    exit 0
}

if ($Action -eq "Update") {
    Assert-ClusteredGraph -ShowInspection
    $beforeInspectionOutput = @(& $pythonPath $inspectScript --graph $graphPath --root $projectRoot 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not read the existing Graphify node count before update."
    }
    $beforeInspection = $beforeInspectionOutput[-1] | ConvertFrom-Json
    Invoke-FullGraphRefresh -ExtractLabel "refresh-extract" -BeforeNodeCount ([int]$beforeInspection.nodes)
    exit 0
}

if ($Action -eq "EnsureFresh") {
    $ensureStartedAt = Get-Date
    $freshness = Get-GraphFreshness
    $rebuildPerformed = $false

    if (-not $freshness.GraphExists) {
        $rebuildPerformed = $true
        Invoke-FullGraphRefresh -ExtractLabel "ensure-extract"
    } else {
        Assert-ClusteredGraph
        if (-not $freshness.IsFresh) {
            $beforeInspectionOutput = @(& $pythonPath $inspectScript --graph $graphPath --root $projectRoot 2>&1)
            if ($LASTEXITCODE -ne 0) {
                throw "Could not read the existing Graphify node count before EnsureFresh."
            }
            $beforeInspection = $beforeInspectionOutput[-1] | ConvertFrom-Json
            $rebuildPerformed = $true
            Invoke-FullGraphRefresh -ExtractLabel "ensure-extract" -BeforeNodeCount ([int]$beforeInspection.nodes)
        }
    }

    $ensureElapsedMilliseconds = [int]((Get-Date) - $ensureStartedAt).TotalMilliseconds
    $ensureOutput = @(
        ("graphify_ensure_fresh: {0}" -f $(if ($rebuildPerformed) { "rebuilt" } else { "fresh" })),
        ("rebuild_performed: {0}" -f $rebuildPerformed.ToString().ToLowerInvariant()),
        ("elapsed_ms: {0}" -f $ensureElapsedMilliseconds)
    )
    $ensureSignals = Get-GraphifyOutputSignals -ActionName "EnsureFresh" -OutputLines $ensureOutput
    Write-GraphifyUsageRecord -RecordedAction "EnsureFresh" -ElapsedMilliseconds $ensureElapsedMilliseconds -ExitCode 0 -Signals $ensureSignals -RebuildPerformed $rebuildPerformed
    $ensureOutput
    Assert-ClusteredGraph -ShowInspection
    exit 0
}

Assert-ClusteredGraph

if ($Action -ne "Status" -and $Action -ne "Diagnose") {
    Assert-GraphFreshness
}

switch ($Action) {
    "Status" {
        Assert-ClusteredGraph -ShowInspection
        exit 0
    }
    "Query" {
        if ([string]::IsNullOrWhiteSpace($Question)) {
            throw "-Question is required for -Action Query."
        }
        $queryArguments = @(
            "query", $Question, "--budget", [string]$Budget, "--graph", $graphPath
        )
        foreach ($contextValue in $Context) {
            if (-not [string]::IsNullOrWhiteSpace($contextValue)) {
                $queryArguments += @("--context", $contextValue)
            }
        }
        Invoke-GraphifyCommand -Label "query" -ShowAll -TrackUsage -ArgumentList $queryArguments
    }
    "Path" {
        if ([string]::IsNullOrWhiteSpace($Source) -or [string]::IsNullOrWhiteSpace($Target)) {
            throw "-Source and -Target are required for -Action Path."
        }
        Invoke-GraphifyCommand -Label "path" -ShowAll -TrackUsage -ArgumentList @(
            "path", $Source, $Target, "--graph", $graphPath
        )
    }
    "Explain" {
        if ([string]::IsNullOrWhiteSpace($Source)) {
            throw "-Source is required for -Action Explain."
        }
        Invoke-GraphifyCommand -Label "explain" -ShowAll -TrackUsage -ArgumentList @(
            "explain", $Source, "--graph", $graphPath
        )
    }
    "Affected" {
        if ([string]::IsNullOrWhiteSpace($Source)) {
            throw "-Source is required for -Action Affected."
        }
        Invoke-GraphifyCommand -Label "affected" -ShowAll -TrackUsage -ArgumentList @(
            "affected", $Source, "--depth", [string]$Depth, "--graph", $graphPath
        )
    }
    "Diagnose" {
        Invoke-GraphifyCommand -Label "diagnose" -ShowAll -ArgumentList @(
            "diagnose", "multigraph", "--graph", $graphPath, "--json"
        )
    }
    "Benchmark" {
        Invoke-GraphifyCommand -Label "benchmark" -ShowAll -ArgumentList @(
            "benchmark", $graphPath
        )
    }
}
