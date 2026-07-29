$ErrorActionPreference = "Stop"

$toolsRoot = Split-Path $PSScriptRoot -Parent
$entryPath = Join-Path $toolsRoot "area-tool.ps1"
$schemaPath = Join-Path $toolsRoot "AreaTool\operations.psd1"

if (-not (Test-Path -LiteralPath $entryPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $schemaPath -PathType Leaf)) {
    throw "The typed area-tool entry or operation schema is missing."
}
if ([System.IO.File]::ReadAllLines($entryPath).Count -gt 650) {
    throw "area-tool exceeded the 650-line dispatcher limit; split internal adapters."
}
if ([System.IO.File]::ReadAllLines($schemaPath).Count -gt 320) {
    throw "area-tool operation schema exceeded 320 lines; group operation metadata."
}

$schema = Import-PowerShellDataFile -LiteralPath $schemaPath
if ([int]$schema.SchemaVersion -ne 1 -or $schema.Operations.Count -lt 20) {
    throw "The area-tool schema version or operation coverage is invalid."
}

$entryCommand = Get-Command $entryPath
foreach ($commonParameter in @("Operation", "Json", "PrintOutput")) {
    if (-not $entryCommand.Parameters.ContainsKey($commonParameter)) {
        throw "area-tool is missing public parameter: $commonParameter"
    }
}

foreach ($operationName in $schema.Operations.Keys) {
    $definition = $schema.Operations[$operationName]
    foreach ($parameterName in @($definition.Required) + @($definition.Allowed)) {
        if (-not $entryCommand.Parameters.ContainsKey($parameterName)) {
            throw "Schema parameter is not typed by area-tool: $operationName -> $parameterName"
        }
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$definition.Implementation)) {
        $implementationPath = Join-Path $toolsRoot $definition.Implementation
        if (-not (Test-Path -LiteralPath $implementationPath -PathType Leaf)) {
            throw "area-tool implementation is missing: $operationName -> $implementationPath"
        }
    }
}

$benchmarkDefinition = $schema.Operations["Benchmark.ReadCost"]
if ($null -eq $benchmarkDefinition -or
    $benchmarkDefinition.Allowed -notcontains "BaselineRef" -or
    $benchmarkDefinition.Allowed -notcontains "ReportPath") {
    throw "Benchmark.ReadCost is not registered with its typed report contract."
}
$benchmarkPath = Join-Path $toolsRoot "read-cost-benchmark.ps1"
@(& $benchmarkPath -SelfTest) | Out-Null

$entryText = [System.IO.File]::ReadAllText($entryPath)
if ($entryText.Contains("Invoke-Expression") -or
    $entryText.Contains("powershell -Command")) {
    throw "area-tool must dispatch by typed splatting, not command-string evaluation."
}
if (-not $entryText.Contains("Out-String -Stream")) {
    throw "area-tool must render PowerShell formatting objects before creating its envelope."
}
if (-not $entryText.Contains("area_tool_data_json: ") -or
    -not $entryText.Contains("data = `$structuredData")) {
    throw "area-tool must promote structured child data into its envelope."
}
$tokenCommonText = [System.IO.File]::ReadAllText(
    (Join-Path $toolsRoot "TokenUsageCommon.ps1")
)
if (-not $entryText.Contains("AREA_TOOL_OPERATION") -or
    -not $tokenCommonText.Contains("area_tool_operation")) {
    throw "area-tool operation telemetry is not connected to TokenReports."
}

$schemaJson = @(
    & $entryPath -Operation Schema -Target Code.Read -Json
) -join "`n"
$schemaResult = $schemaJson | ConvertFrom-Json
if ($schemaResult.operations.Count -ne 1 -or
    $schemaResult.operations[0].operation -ne "Code.Read" -or
    $schemaResult.operations[0].required -notcontains "Path") {
    throw "area-tool schema query did not return the typed Code.Read contract."
}

$rejected = $false
try {
    & $entryPath `
        -Operation Code.Symbol `
        -Symbol GameManager `
        -Path "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.cs" |
        Out-Null
} catch {
    $rejected = $_.Exception.Message -match "does not accept -Path"
}
if (-not $rejected) {
    throw "area-tool did not reject an operation-specific extra parameter."
}

$converterPath = Join-Path $toolsRoot "Convert-AreaCommandToSafe.ps1"
$converterOutput = @(
    & $converterPath -Command "git diff" |
        Out-String -Stream
) -join "`n"
if (-not $converterOutput.Contains("area-tool.ps1 -Operation Git.Diff") -or
    $converterOutput.Contains("safe-diff.ps1")) {
    throw "Raw command conversion did not route to the typed area-tool entry."
}

foreach ($obsoleteEntry in @(
        "guarded-command.ps1",
        "Import-AreaTokenAliases.ps1"
    )) {
    if (Test-Path -LiteralPath (Join-Path $toolsRoot $obsoleteEntry)) {
        throw "Obsolete public wrapper entry must not be restored: $obsoleteEntry"
    }
}

$publicFiles = @(
    Join-Path (Split-Path (Split-Path $toolsRoot -Parent) -Parent) "AGENTS.md"
    Join-Path (Split-Path (Split-Path $toolsRoot -Parent) -Parent) "Docs"
    Join-Path $toolsRoot "README.md"
)
foreach ($publicPath in $publicFiles) {
    $files = if (Test-Path -LiteralPath $publicPath -PathType Container) {
        @(Get-ChildItem -LiteralPath $publicPath -Filter "*.md" -File -Recurse)
    } else {
        @(Get-Item -LiteralPath $publicPath)
    }
    foreach ($file in $files) {
        $text = [System.IO.File]::ReadAllText($file.FullName)
        if ($text -match "Tools/TokenUsage/(?:safe-|Invoke-AreaSafe|structure-index|token-report-summary)") {
            throw "Public documentation bypasses area-tool: $($file.FullName)"
        }
    }
}

$symbolJson = @(
    & $entryPath `
        -Operation Code.Symbol `
        -Symbol AddExperience `
        -Language CSharp `
        -MaxResults 1 `
        -Json
) -join "`n"
$symbolResult = $symbolJson | ConvertFrom-Json
if ($symbolResult.operation -ne "Code.Symbol" -or
    $symbolResult.status -ne "success" -or
    [int]$symbolResult.result_count -lt 1) {
    throw "area-tool Code.Symbol did not return a successful typed envelope."
}

Write-Output "command_tool_test_module: area-tool passed"
