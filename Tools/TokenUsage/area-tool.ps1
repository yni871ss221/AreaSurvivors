[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Operation,

    [string]$Target = "",
    [string[]]$Path = @(),
    [string]$Symbol = "",
    [string]$Pattern = "",
    [string]$Ranges = "",
    [string]$RefRange = "",
    [string]$Extension = "",
    [string]$CommandText = "",
    [string]$MenuPath = "",
    [string]$Phase = "",
    [string]$ScriptPath = "",
    [string]$DependencyPaths = "",
    [string]$Task = "",
    [string]$CoverageNote = "",
    [string]$ReportName = "",
    [string]$BaselineRef = "",
    [string]$ReportPath = "",
    [string]$ExpectedHash = "",
    [string]$Purpose = "",
    [string]$Flow = "",
    [string]$Source = "",
    [string]$Question = "",
    [string[]]$GraphContext = @(),
    [string[]]$Invariants = @(),
    [string[]]$SideEffects = @(),
    [string[]]$Verification = @(),

    [ValidateSet("All", "CSharp", "PowerShell")]
    [string]$Language = "All",

    [ValidateSet("Matches", "Files", "Summary")]
    [string]$SearchMode = "Matches",

    [ValidateSet("Summary", "Stat", "Names", "Full")]
    [string]$DiffMode = "Summary",

    [ValidateSet("Log", "Error", "Warning")]
    [string]$ConsoleLevel = "Error",

    [ValidateSet("Enter", "Exit", "Status")]
    [string]$PlayAction = "Status",

    [int]$StartLine = 0,
    [int]$EndLine = 0,
    [int]$Last = 0,
    [int]$Context = 0,
    [int]$MaxResults = 0,
    [int]$TimeoutSeconds = 0,
    [int]$CompileWaitSeconds = 0,
    [int]$ImportTimeoutSeconds = 0,
    [int]$MenuTimeoutSeconds = 0,
    [int]$ResultWaitSeconds = 0,
    [int]$Days = 0,
    [int]$Recent = 0,
    [int]$BudgetTokens = 0,
    [int]$Depth = 0,
    [int]$Budget = 0,

    [double]$UiPercent = -1,
    [double]$CurrentPercent = -1,
    [double]$StartPercent = -1,

    [switch]$Literal,
    [switch]$Force,
    [switch]$Cached,
    [switch]$ExcludeUnityMeta,
    [switch]$DryRun,
    [switch]$ExecuteOriginalIfSafe,
    [switch]$BatchRefresh,
    [switch]$Concise,
    [switch]$SinceLastStart,
    [switch]$FailedOnly,
    [switch]$IncludeUnity,
    [switch]$PrintOutput,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$toolRoot = $PSScriptRoot
$schemaPath = Join-Path $toolRoot "AreaTool\operations.psd1"
$schema = Import-PowerShellDataFile -LiteralPath $schemaPath
$operations = $schema.Operations

if ($PSBoundParameters.ContainsKey("Path")) {
    $expandedPaths = New-Object System.Collections.Generic.List[string]
    foreach ($pathItem in $Path) {
        foreach ($part in ($pathItem -split ";")) {
            $trimmedPart = $part.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmedPart)) {
                $expandedPaths.Add($trimmedPart)
            }
        }
    }
    $Path = @($expandedPaths)
    $PSBoundParameters["Path"] = $Path
}

function Test-NonEmptyValue {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)]$Value
    )

    if ($Value -is [string]) {
        return -not [string]::IsNullOrWhiteSpace($Value)
    }
    if ($Value -is [array]) {
        return $Value.Count -gt 0
    }
    return $true
}

function Add-IfBound {
    param(
        [Parameter(Mandatory = $true)][hashtable]$Arguments,
        [Parameter(Mandatory = $true)][string]$SourceName,
        [string]$TargetName = ""
    )

    if (-not $PSBoundParametersRoot.ContainsKey($SourceName)) {
        return
    }
    if ([string]::IsNullOrWhiteSpace($TargetName)) {
        $TargetName = $SourceName
    }
    $Arguments[$TargetName] = $PSBoundParametersRoot[$SourceName]
}

function Write-Schema {
    param([string]$RequestedOperation)

    $items = @()
    foreach ($name in @($operations.Keys | Sort-Object)) {
        if (-not [string]::IsNullOrWhiteSpace($RequestedOperation) -and
            -not [string]::Equals(
                $name,
                $RequestedOperation,
                [System.StringComparison]::OrdinalIgnoreCase
            )) {
            continue
        }
        $definition = $operations[$name]
        $items += [pscustomobject]@{
            operation = $name
            risk = [string]$definition.Risk
            required = @($definition.Required)
            allowed = @($definition.Allowed)
        }
    }

    if ($items.Count -eq 0) {
        throw "Unknown area-tool schema target: $RequestedOperation"
    }

    $result = [pscustomobject]@{
        schema_version = [int]$schema.SchemaVersion
        operations = $items
    }
    if ($Json) {
        $result | ConvertTo-Json -Depth 6
        return
    }

    Write-Output ("area_tool_schema_version: {0}" -f $schema.SchemaVersion)
    foreach ($item in $items) {
        Write-Output (
            "{0}: risk={1}; required={2}; allowed={3}" -f
            $item.operation,
            $item.risk,
            (@($item.required) -join ","),
            (@($item.allowed) -join ",")
        )
    }
}

function Invoke-Implementation {
    param(
        [Parameter(Mandatory = $true)][string]$ScriptName,
        [Parameter(Mandatory = $true)][hashtable]$Arguments
    )

    $scriptPath = Join-Path $toolRoot $ScriptName
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw "area-tool implementation is missing: $ScriptName"
    }

    $capturedOutput = New-Object System.Collections.Generic.List[string]
    $exitCode = 0
    $previousOperation = $env:AREA_TOOL_OPERATION
    try {
        $env:AREA_TOOL_OPERATION = $Operation
        $rawOutput = @(
            & $scriptPath @Arguments 2>&1 |
                Out-String -Stream
        )
        foreach ($item in $rawOutput) {
            if ($null -ne $item) {
                $capturedOutput.Add([string]$item)
            }
        }
        if ($LASTEXITCODE -is [int]) {
            $exitCode = [int]$LASTEXITCODE
        }
    } catch {
        $capturedOutput.Add($_.Exception.Message)
        $exitCode = 1
    } finally {
        if ($null -eq $previousOperation) {
            Remove-Item Env:AREA_TOOL_OPERATION -ErrorAction SilentlyContinue
        } else {
            $env:AREA_TOOL_OPERATION = $previousOperation
        }
    }

    return [pscustomobject]@{
        exit_code = $exitCode
        output = @($capturedOutput)
    }
}

if (-not $operations.ContainsKey($Operation)) {
    $known = @($operations.Keys | Sort-Object) -join ", "
    throw "Unknown area-tool operation '$Operation'. Known operations: $known"
}

$definition = $operations[$Operation]
$commonParameters = @("Operation", "Json")
$boundOperationParameters = @(
    $PSBoundParameters.Keys |
        Where-Object { $commonParameters -notcontains $_ }
)
foreach ($name in $boundOperationParameters) {
    if (@($definition.Allowed) -notcontains $name) {
        throw "Operation $Operation does not accept -$name. Use -Operation Schema -Target $Operation."
    }
}
foreach ($requiredName in @($definition.Required)) {
    if (-not $PSBoundParameters.ContainsKey($requiredName) -or
        -not (Test-NonEmptyValue -Name $requiredName -Value $PSBoundParameters[$requiredName])) {
        throw "Operation $Operation requires -$requiredName."
    }
}
if ([bool]$definition.SinglePath -and $Path.Count -ne 1) {
    throw "Operation $Operation requires exactly one -Path value."
}

if ($Operation -eq "Schema") {
    Write-Schema -RequestedOperation $Target
    return
}

if ($Operation -eq "Code.Read") {
    $selectors = 0
    foreach ($selector in @("Ranges", "Last", "Pattern", "StartLine", "EndLine")) {
        if ($PSBoundParameters.ContainsKey($selector)) {
            $selectors++
        }
    }
    if ($PSBoundParameters.ContainsKey("Ranges") -and $selectors -gt 1) {
        throw "Code.Read -Ranges cannot be combined with line, tail, or pattern selectors."
    }
    if (($PSBoundParameters.ContainsKey("StartLine") -xor
            $PSBoundParameters.ContainsKey("EndLine"))) {
        throw "Code.Read requires -StartLine and -EndLine together."
    }
}
if ($Operation -eq "Code.Search" -and
    $SearchMode -ne "Matches" -and
    $PSBoundParameters.ContainsKey("Context")) {
    throw "Code.Search -Context is only valid with -SearchMode Matches."
}

$PSBoundParametersRoot = $PSBoundParameters
$implementation = [string]$definition.Implementation
$arguments = @{}

switch ($Operation) {
    "Code.Symbol" {
        $arguments.Action = "Query"
        $arguments.Symbol = $Symbol
        $arguments.Language = $Language
        if ($MaxResults -gt 0) { $arguments.MaxResults = $MaxResults }
        if ($Force) { $arguments.Force = $true }
    }
    "Code.File" {
        $arguments.Path = $Path[0]
        $arguments.Language = $Language
        if ($MaxResults -gt 0) { $arguments.MaxResults = $MaxResults }
        if ($Force) { $arguments.Force = $true }
    }
    "Code.Summary.Store" {
        $arguments.Action = "Store"
        $arguments.Path = $Path[0]
        $arguments.ExpectedHash = $ExpectedHash
        $arguments.Purpose = $Purpose
        if (-not [string]::IsNullOrWhiteSpace($Flow)) {
            $arguments.Flow = $Flow
        }
        if ($Invariants.Count -gt 0) {
            $arguments.Invariants = $Invariants
        }
        if ($SideEffects.Count -gt 0) {
            $arguments.SideEffects = $SideEffects
        }
        if ($Verification.Count -gt 0) {
            $arguments.Verification = $Verification
        }
    }
    "Code.Summary.Stats" {
        $arguments.Action = "Stats"
        if ($MaxResults -gt 0) { $arguments.Top = $MaxResults }
    }
    "Code.Read" {
        if ($PSBoundParameters.ContainsKey("Ranges")) {
            $implementation = "safe-read-batch.ps1"
            $arguments.Path = $Path[0]
            $arguments.Ranges = $Ranges
            if ($PrintOutput) { $arguments.PrintOutput = $true }
        } else {
            $arguments.Path = $Path[0]
            Add-IfBound -Arguments $arguments -SourceName "StartLine"
            Add-IfBound -Arguments $arguments -SourceName "EndLine"
            Add-IfBound -Arguments $arguments -SourceName "Last"
            if ($PSBoundParameters.ContainsKey("Pattern")) {
                $arguments.Pattern = $Pattern
            }
            Add-IfBound -Arguments $arguments -SourceName "Context"
            if ($MaxResults -gt 0) { $arguments.MaxMatches = $MaxResults }
            if ($Literal) { $arguments.LiteralPattern = $true }
            if ($PrintOutput) { $arguments.PrintOutput = $true }
        }
    }
    "Code.Search" {
        $effectivePattern = $Pattern
        if ($Literal) { $effectivePattern = [regex]::Escape($Pattern) }
        if ($Context -gt 0) {
            $implementation = "focused-search.ps1"
            $arguments.Pattern = $effectivePattern
            if ($Path.Count -gt 0) { $arguments.Path = $Path }
            $arguments.Context = $Context
            if ($MaxResults -gt 0) { $arguments.MaxMatchesPerFile = $MaxResults }
            if (-not [string]::IsNullOrWhiteSpace($Extension)) {
                $arguments.Extension = $Extension
            }
            if ($PrintOutput) { $arguments.PrintOutput = $true }
        } else {
            $arguments.Pattern = $effectivePattern
            if ($Path.Count -gt 0) { $arguments.Path = $Path }
            if ($MaxResults -gt 0) { $arguments.First = $MaxResults }
            if (-not [string]::IsNullOrWhiteSpace($Extension)) {
                $arguments.Extension = $Extension
            }
            if ($SearchMode -eq "Files") { $arguments.FilesOnly = $true }
            if ($SearchMode -eq "Summary") { $arguments.HitSummary = $true }
            if ($PrintOutput) { $arguments.PrintOutput = $true }
        }
    }
    "Git.Diff" {
        if ($Path.Count -gt 0) { $arguments.Path = $Path }
        if (-not [string]::IsNullOrWhiteSpace($RefRange)) {
            $arguments.RefRange = $RefRange
        }
        if ($DiffMode -eq "Summary") { $arguments.SummaryOnly = $true }
        if ($DiffMode -eq "Stat") { $arguments.Stat = $true }
        if ($DiffMode -eq "Names") { $arguments.NameOnly = $true }
        if ($MaxResults -gt 0) { $arguments.MaxLines = $MaxResults }
        if ($PrintOutput -or $DiffMode -eq "Full") { $arguments.PrintOutput = $true }
    }
    "Git.Check" {
        $arguments.Path = $Path
        if ($Cached) { $arguments.Cached = $true }
        if ($ExcludeUnityMeta) { $arguments.ExcludeUnityMeta = $true }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Git.Status" {
        if ($Path.Count -gt 0) { $arguments.Path = $Path }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Git.Log" {
        $arguments.Action = "Log"
        if ($MaxResults -gt 0) { $arguments.First = $MaxResults }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Command.Guard" {
        $arguments.Command = $CommandText
        if ($DryRun) { $arguments.DryRun = $true }
        if ($ExecuteOriginalIfSafe) { $arguments.ExecuteOriginalIfSafe = $true }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Graph.Status" {
        $arguments.Action = "Status"
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Graph.Ensure" {
        $arguments.Action = "EnsureFresh"
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Graph.Update" {
        $arguments.Action = "Update"
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Graph.Explain" {
        $arguments.Action = "Explain"
        $arguments.Source = $Source
        if ($Budget -gt 0) { $arguments.Budget = $Budget }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Graph.Path" {
        $arguments.Action = "Path"
        $arguments.Source = $Source
        $arguments.Target = $Target
        if ($Budget -gt 0) { $arguments.Budget = $Budget }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Graph.Affected" {
        $arguments.Action = "Affected"
        $arguments.Source = $Source
        if ($Depth -gt 0) { $arguments.Depth = $Depth }
        if ($MaxResults -gt 0) { $arguments.AffectedDisplayLimit = $MaxResults }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Graph.Query" {
        $arguments.Action = "Query"
        $arguments.Question = $Question
        if ($GraphContext.Count -gt 0) { $arguments.Context = $GraphContext }
        if ($Budget -gt 0) { $arguments.Budget = $Budget }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Unity.Console" {
        $arguments.Action = switch ($ConsoleLevel) {
            "Log" { "ConsoleLogs" }
            "Warning" { "ConsoleWarnings" }
            default { "ConsoleErrors" }
        }
        if ($MaxResults -gt 0) { $arguments.MaxCount = $MaxResults }
        if ($TimeoutSeconds -gt 0) { $arguments.TimeoutSeconds = $TimeoutSeconds }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Unity.Compile" {
        $arguments.Action = "Compile"
        if ($TimeoutSeconds -gt 0) { $arguments.TimeoutSeconds = $TimeoutSeconds }
        if ($CompileWaitSeconds -gt 0) {
            $arguments.CompileWaitSeconds = $CompileWaitSeconds
        }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Unity.Menu" {
        $arguments.Action = "Menu"
        $arguments.MenuPath = $MenuPath
        if ($TimeoutSeconds -gt 0) { $arguments.TimeoutSeconds = $TimeoutSeconds }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Unity.Import" {
        $arguments.Action = "AssetImport"
        $arguments.AssetPath = $Path[0]
        if ($TimeoutSeconds -gt 0) { $arguments.TimeoutSeconds = $TimeoutSeconds }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Unity.Refresh" {
        $arguments.Action = "AssetRefresh"
        if ($TimeoutSeconds -gt 0) { $arguments.TimeoutSeconds = $TimeoutSeconds }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Unity.Search" {
        $arguments.Query = $Pattern
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Unity.Report" {
        $arguments.Report = $ReportName
    }
    "Unity.Validate" {
        $arguments.MenuPath = $MenuPath
        if ($ResultWaitSeconds -gt 0) {
            $arguments.ResultWaitSeconds = $ResultWaitSeconds
        }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Unity.Runner" {
        $arguments.Phase = $Phase
        $arguments.ScriptPath = $ScriptPath
        if (-not [string]::IsNullOrWhiteSpace($DependencyPaths)) {
            $arguments.DependencyScriptPaths = $DependencyPaths
        }
        if (-not [string]::IsNullOrWhiteSpace($MenuPath)) {
            $arguments.MenuPath = $MenuPath
        }
        if ($ImportTimeoutSeconds -gt 0) {
            $arguments.ImportTimeoutSeconds = $ImportTimeoutSeconds
        }
        if ($TimeoutSeconds -gt 0) {
            $arguments.CompileTimeoutSeconds = $TimeoutSeconds
        }
        if ($MenuTimeoutSeconds -gt 0) {
            $arguments.MenuTimeoutSeconds = $MenuTimeoutSeconds
        }
        if ($BatchRefresh) { $arguments.BatchRefresh = $true }
        if ($Concise) { $arguments.Concise = $true }
    }
    "Unity.Play" {
        $arguments.Action = switch ($PlayAction) {
            "Enter" { "PlayEnter" }
            "Exit" { "PlayExit" }
            default { "PlayStatus" }
        }
        if ($TimeoutSeconds -gt 0) { $arguments.TimeoutSeconds = $TimeoutSeconds }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
    }
    "Token.Summary" {
        if ($Days -gt 0) { $arguments.Days = $Days }
        if ($Recent -gt 0) { $arguments.Recent = $Recent }
        if ($SinceLastStart) { $arguments.SinceLastStart = $true }
        if ($FailedOnly) { $arguments.FailedOnly = $true }
        if ($MaxResults -gt 0) { $arguments.Top = $MaxResults }
    }
    "Token.Start" {
        $arguments.Task = $Task
        if ($UiPercent -ge 0) { $arguments.UiPercent = $UiPercent }
        if ($BudgetTokens -gt 0) { $arguments.BudgetTokens = $BudgetTokens }
        if ($IncludeUnity) { $arguments.IncludeUnity = $true }
    }
    "Token.End" {
        if ($CurrentPercent -ge 0) { $arguments.CurrentPercent = $CurrentPercent }
        if ($StartPercent -ge 0) { $arguments.StartPercent = $StartPercent }
        if ($BudgetTokens -gt 0) { $arguments.BudgetTokens = $BudgetTokens }
        if (-not [string]::IsNullOrWhiteSpace($CoverageNote)) {
            $arguments.CoverageNote = $CoverageNote
        }
        if ($IncludeUnity) { $arguments.IncludeUnity = $true }
    }
    "Project.Weight" {
        if ($MaxResults -gt 0) { $arguments.Top = $MaxResults }
    }
    "Benchmark.ReadCost" {
        if (-not [string]::IsNullOrWhiteSpace($BaselineRef)) {
            $arguments.BaselineRef = $BaselineRef
        }
        if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
            $arguments.ReportPath = $ReportPath
        }
    }
    "Benchmark.SummaryCache" {
        if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
            $arguments.ReportPath = $ReportPath
        }
    }
    "Test.Commands" {
    }
}

$execution = Invoke-Implementation -ScriptName $implementation -Arguments $arguments
$structuredData = $null
$visibleOutput = @(
    foreach ($outputLine in @($execution.output)) {
        $lineText = [string]$outputLine
        if ($lineText.StartsWith(
                "area_tool_data_json: ",
                [System.StringComparison]::Ordinal
            )) {
            $dataJson = $lineText.Substring("area_tool_data_json: ".Length)
            $structuredData = $dataJson | ConvertFrom-Json
            continue
        }
        $outputLine
    }
)
$outputText = @($visibleOutput)
$combined = $outputText -join "`n"
$status = "success"
if ([int]$execution.exit_code -ne 0) {
    if ($combined -match "guard_code\s*:") {
        $status = "guarded"
    } else {
        $status = "failed"
    }
}

$capturePath = ""
$captureMatch = [regex]::Match(
    $combined,
    "(?m)^(?:captured_to|capture_path|validator_result_path):\s*(.+)$"
)
if ($captureMatch.Success) {
    $capturePath = $captureMatch.Groups[1].Value.Trim()
}

$displayedTokens = $null
$tokenMatch = [regex]::Match($combined, "(?m)^estimated_tokens:\s*(\d+)$")
if ($tokenMatch.Success) {
    $displayedTokens = [int]$tokenMatch.Groups[1].Value
}

$resultCount = $null
foreach ($patternValue in @(
        "(?m)^console_matched_count:\s*(\d+)$",
        "(?m)^validator_check_count:\s*(\d+)$",
        "(?m)^scenario_count:\s*(\d+)$",
        "(?m)^summary_cache_entries:\s*(\d+)$",
        "(?m)^summary_cache_benchmark_entries:\s*(\d+)$",
        "(?m)^definitions:\s*(\d+)",
        "(?m)^matched_files:\s*(\d+)",
        "(?m)^structure_index_status:.*sources=(\d+)"
    )) {
    $countMatch = [regex]::Match($combined, $patternValue)
    if ($countMatch.Success) {
        $resultCount = [int]$countMatch.Groups[1].Value
        break
    }
}

$envelope = [pscustomobject]@{
    schema_version = [int]$schema.SchemaVersion
    operation = $Operation
    risk = [string]$definition.Risk
    status = $status
    exit_code = [int]$execution.exit_code
    result_count = $resultCount
    capture_path = $capturePath
    displayed_estimated_tokens = $displayedTokens
    data = $structuredData
    output = $outputText
}

if ($Json) {
    $envelope | ConvertTo-Json -Depth 6
} else {
    foreach ($line in $outputText) {
        Write-Output $line
    }
    Write-Output (
        "area_tool_result: operation={0}; status={1}; exit_code={2}" -f
        $Operation,
        $status,
        $execution.exit_code
    )
}

$global:LASTEXITCODE = [int]$execution.exit_code
if ([int]$execution.exit_code -ne 0) {
    exit ([int]$execution.exit_code)
}
