[CmdletBinding()]
param(
    [string]$BaselineRef = "HEAD",
    [string]$ReportPath = "",
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$commonPath = Join-Path $PSScriptRoot "TokenUsageCommon.ps1"
$structureIndexPath = Join-Path $PSScriptRoot "structure-index.ps1"
. $commonPath

if ($BaselineRef -notmatch "^[A-Za-z0-9._/-]+$") {
    throw "BaselineRef contains unsupported characters: $BaselineRef"
}

$structurePaths = @(
    "Assets/AreaSurvivors/Scripts/Game/Characters/EnemyController.cs",
    "Assets/AreaSurvivors/Scripts/Game/Characters/PlayerStats.cs",
    "Assets/AreaSurvivors/Scripts/Game/Weapons/WeaponController.cs"
)
$gameManagerBaselinePath =
    "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.cs"
$responsibilityPaths = @(
    "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.LevelProgression.cs",
    "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.UpgradeChoices.cs",
    "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.LevelUpPanel.cs",
    "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.RunStage.cs",
    "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.RelicModal.cs",
    "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.RunEnd.cs",
    "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.StageHud.cs"
)

function Get-WorkingTreeText {
    param([Parameter(Mandatory = $true)][string]$RepoPath)

    $absolutePath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $RepoPath))
    $rootPrefix = $projectRoot.TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar
    if (-not $absolutePath.StartsWith(
            $rootPrefix,
            [System.StringComparison]::OrdinalIgnoreCase
        )) {
        throw "Working-tree path escaped the project root: $RepoPath"
    }
    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
        throw "Working-tree benchmark file is missing: $RepoPath"
    }
    return [System.IO.File]::ReadAllText($absolutePath)
}

function Invoke-GitText {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $gitCommand = Get-Command git.exe -ErrorAction Stop
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $gitCommand.Source
    $startInfo.WorkingDirectory = $projectRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true
    $startInfo.StandardOutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $startInfo.StandardErrorEncoding = New-Object System.Text.UTF8Encoding($false)
    $startInfo.Arguments = ($Arguments | ForEach-Object {
            '"' + $_.Replace('"', '\"') + '"'
        }) -join " "

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "git $($Arguments[0]) failed: $($stderr.Trim())"
    }
    return $stdout
}

function Get-BaselineText {
    param([Parameter(Mandatory = $true)][string]$RepoPath)

    return Invoke-GitText -Arguments @(
        "show",
        ("{0}:{1}" -f $BaselineRef, $RepoPath)
    )
}

function Get-StructureOutput {
    param([Parameter(Mandatory = $true)][string]$RepoPath)

    $discard = @(
        & $structureIndexPath `
            -Action Query `
            -Path $RepoPath `
            -Language CSharp `
            -MaxResults 20
    )
    $output = @(
        & $structureIndexPath `
            -Action Query `
            -Path $RepoPath `
            -Language CSharp `
            -MaxResults 20
    )
    if ($output.Count -eq 0 -or
        ($output -join "`n") -notmatch "(?m)^matched_files:\s*1$") {
        throw "Code.File failed for benchmark path: $RepoPath"
    }
    return (@($output) + @(
            "area_tool_result: operation=Code.File; status=success; exit_code=0"
        )) -join "`n"
}

function New-ReadScenario {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$BaselineSource,
        [Parameter(Mandatory = $true)][string]$CurrentSource,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$BaselineText,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$CurrentText
    )

    $baselineEstimate =
        Get-TokenUsageEstimate -Source $BaselineSource -Text $BaselineText
    $currentEstimate =
        Get-TokenUsageEstimate -Source $CurrentSource -Text $CurrentText
    $saved = [int]$baselineEstimate.estimated_tokens -
        [int]$currentEstimate.estimated_tokens
    $reduction = if ([int]$baselineEstimate.estimated_tokens -eq 0) {
        0
    } else {
        [math]::Round(
            100.0 * $saved / [int]$baselineEstimate.estimated_tokens,
            1
        )
    }

    return [pscustomobject][ordered]@{
        category = $Category
        name = $Name
        baseline_source = $BaselineSource
        current_source = $CurrentSource
        baseline_lines = [int]$baselineEstimate.lines
        current_lines = [int]$currentEstimate.lines
        baseline_estimated_tokens = [int]$baselineEstimate.estimated_tokens
        current_estimated_tokens = [int]$currentEstimate.estimated_tokens
        saved_estimated_tokens = $saved
        reduction_percent = $reduction
    }
}

function New-Summary {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][object[]]$Items
    )

    $baseline = [int](($Items |
                Measure-Object -Property baseline_estimated_tokens -Sum).Sum)
    $current = [int](($Items |
                Measure-Object -Property current_estimated_tokens -Sum).Sum)
    $saved = $baseline - $current
    $reduction = if ($baseline -eq 0) {
        0
    } else {
            [math]::Round(100.0 * $saved / $baseline, 1)
    }
    $meanReduction = [math]::Round(
        [double](($Items |
                    Measure-Object -Property reduction_percent -Average).Average),
        1
    )

    return [pscustomobject][ordered]@{
        category = $Name
        scenario_count = $Items.Count
        baseline_estimated_tokens = $baseline
        current_estimated_tokens = $current
        saved_estimated_tokens = $saved
        reduction_percent = $reduction
        scenario_mean_reduction_percent = $meanReduction
    }
}

$requiredCurrentPaths = @("AGENTS.md") + $structurePaths + $responsibilityPaths
foreach ($requiredPath in $requiredCurrentPaths) {
    [void](Get-WorkingTreeText -RepoPath $requiredPath)
}
[void](Get-BaselineText -RepoPath "AGENTS.md")
[void](Get-BaselineText -RepoPath $gameManagerBaselinePath)
foreach ($structurePath in $structurePaths) {
    [void](Get-BaselineText -RepoPath $structurePath)
}

if ($SelfTest) {
    $probe = Get-TokenUsageEstimate -Source "self-test" -Text "AreaSurvivors"
    if ($structurePaths.Count -ne 3 -or
        $responsibilityPaths.Count -ne 7 -or
        [int]$probe.estimated_tokens -le 0) {
        throw "Read-cost benchmark scenario contract is invalid."
    }
    Write-Output "read_cost_benchmark_self_test: passed"
    return
}

$resolvedBaseline =
    (Invoke-GitText -Arguments @("rev-parse", $BaselineRef)).Trim()
$scenarios = New-Object System.Collections.Generic.List[object]

$scenarios.Add((New-ReadScenario `
            -Category "constant_context" `
            -Name "AGENTS.md always-loaded context" `
            -BaselineSource "$BaselineRef`:AGENTS.md" `
            -CurrentSource "working-tree:AGENTS.md" `
            -BaselineText (Get-BaselineText -RepoPath "AGENTS.md") `
            -CurrentText (Get-WorkingTreeText -RepoPath "AGENTS.md")))

foreach ($structurePath in $structurePaths) {
    $scenarios.Add((New-ReadScenario `
                -Category "structure_index" `
                -Name ("Structure lookup: " + [System.IO.Path]::GetFileName($structurePath)) `
                -BaselineSource "$BaselineRef`:$structurePath (full file)" `
                -CurrentSource "Code.File structure-index output" `
                -BaselineText (Get-BaselineText -RepoPath $structurePath) `
                -CurrentText (Get-StructureOutput -RepoPath $structurePath)))
}

foreach ($responsibilityPath in $responsibilityPaths) {
    $scenarios.Add((New-ReadScenario `
                -Category "responsibility_split" `
                -Name ("GameManager responsibility: " +
                    [System.IO.Path]::GetFileNameWithoutExtension($responsibilityPath)) `
                -BaselineSource "$BaselineRef`:$gameManagerBaselinePath (full file)" `
                -CurrentSource "working-tree:$responsibilityPath" `
                -BaselineText (Get-BaselineText -RepoPath $gameManagerBaselinePath) `
                -CurrentText (Get-WorkingTreeText -RepoPath $responsibilityPath)))
}

$categoryOrder = @(
    "constant_context",
    "structure_index",
    "responsibility_split"
)
$categorySummaries = @(
    foreach ($category in $categoryOrder) {
        $items = @($scenarios | Where-Object { $_.category -eq $category })
        New-Summary -Name $category -Items $items
    }
)
$scenarioArray = @($scenarios | ForEach-Object { $_ })
$totalSummary = New-Summary -Name "combined_fixed_suite" -Items $scenarioArray

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $reportDirectory = Join-Path $projectRoot "TokenReports\Benchmarks"
    $ReportPath = Join-Path $reportDirectory (
        "read-cost-{0}.json" -f (Get-Date).ToString("yyyyMMdd-HHmmss")
    )
} elseif (-not [System.IO.Path]::IsPathRooted($ReportPath)) {
    $ReportPath = Join-Path $projectRoot $ReportPath
}

$reportDirectoryPath = Split-Path $ReportPath -Parent
if (-not (Test-Path -LiteralPath $reportDirectoryPath)) {
    New-Item -ItemType Directory -Path $reportDirectoryPath -Force | Out-Null
}

$report = [pscustomobject][ordered]@{
    schema_version = 1
    benchmark = "read_cost_fixed_suite"
    generated_at = (Get-Date).ToString("o")
    baseline_ref = $BaselineRef
    baseline_commit = $resolvedBaseline
    current_source = "working_tree"
    estimator = "TokenUsageCommon.Get-TokenUsageEstimate"
    coverage = @(
        "AGENTS.md always-loaded text",
        "Three unchanged source files: full-file read versus Code.File output",
        "Seven GameManager task reads: monolith versus responsibility partial"
    )
    exclusions = @(
        "chat history",
        "model reasoning",
        "tool metadata",
        "image input",
        "filesystem and command latency",
        "real-world task frequency"
    )
    categories = $categorySummaries
    total = $totalSummary
    scenarios = $scenarioArray
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText(
    $ReportPath,
    ($report | ConvertTo-Json -Depth 8),
    $utf8NoBom
)

Write-Output "read_cost_benchmark: fixed comparison suite"
Write-Output "baseline_commit: $resolvedBaseline"
Write-Output "scenario_count: $($scenarios.Count)"
foreach ($summary in $categorySummaries + @($totalSummary)) {
    Write-Output (
        "measure: {0}; scenarios={1}; baseline={2}; current={3}; saved={4}; weighted_reduction={5}%; scenario_mean_reduction={6}%" -f
        $summary.category,
        $summary.scenario_count,
        $summary.baseline_estimated_tokens,
        $summary.current_estimated_tokens,
        $summary.saved_estimated_tokens,
        $summary.reduction_percent,
        $summary.scenario_mean_reduction_percent
    )
}
Write-Output "report_path: $ReportPath"
Write-Output (
    "coverage_note: displayed text estimate only; excludes chat, reasoning, tool metadata, images, latency, and real task frequency"
)
