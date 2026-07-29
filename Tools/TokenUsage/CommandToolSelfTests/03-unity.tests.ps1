$ErrorActionPreference = "Stop"

$toolsRoot = Split-Path $PSScriptRoot -Parent

$safeUnityCommand = Get-Command (Join-Path $toolsRoot "safe-unity.ps1")
foreach ($name in @("Action", "MaxCount")) {
    if (-not $safeUnityCommand.Parameters.ContainsKey($name)) {
        throw "safe-unity is missing required parameter: $name"
    }
}

$unitySearchCommand = Get-Command (Join-Path $toolsRoot "safe-unity-search.ps1")
if (-not $unitySearchCommand.Parameters.ContainsKey("Query") -or
    $unitySearchCommand.Parameters.ContainsKey("Path")) {
    throw "safe-unity-search must expose Query and reject Path."
}

$menuCommand = Get-Command (Join-Path $toolsRoot "invoke-menu-validator.ps1")
foreach ($name in @("MenuPath", "ResultWaitSeconds", "SelfTest")) {
    if (-not $menuCommand.Parameters.ContainsKey($name)) {
        throw "invoke-menu-validator is missing required parameter: $name"
    }
}
if ($menuCommand.Parameters.ContainsKey("SuccessMarkerPath")) {
    throw "invoke-menu-validator must not expose the legacy marker contract."
}

$menuSelfTest = @(
    & (Join-Path $toolsRoot "invoke-menu-validator.ps1") -SelfTest
) -join "`n"
if ($menuSelfTest -notmatch "menu_validator_self_test: passed") {
    throw "Structured menu validator contract self-test failed."
}

$projectRoot = Split-Path (Split-Path $toolsRoot -Parent) -Parent
$bridgePath = Join-Path $projectRoot `
    "Assets\AreaSurvivors\Editor\AreaValidationBridge.cs"
if (-not (Test-Path -LiteralPath $bridgePath -PathType Leaf)) {
    throw "AreaValidationBridge.cs is missing."
}
$bridgeText = [System.IO.File]::ReadAllText($bridgePath)
foreach ($requiredContract in @(
        "run_id",
        "validator_id",
        "status",
        "failed_count",
        "public static bool Require",
        "Area Survivors/Internal/Execute Structured Validator Request"
    )) {
    if (-not $bridgeText.Contains($requiredContract)) {
        throw "AreaValidationBridge contract is missing: $requiredContract"
    }
}

$runnerCommand = Get-Command (Join-Path $toolsRoot "invoke-unity-editor-runner.ps1")
foreach ($name in @(
        "Phase",
        "ScriptPath",
        "DependencyScriptPaths",
        "Concise",
        "BatchRefresh"
    )) {
    if (-not $runnerCommand.Parameters.ContainsKey($name)) {
        throw "invoke-unity-editor-runner is missing required parameter: $name"
    }
}

$safeUnityText = [System.IO.File]::ReadAllText(
    (Join-Path $toolsRoot "Invoke-AreaSafeUnity.ps1")
)
foreach ($sentinel in @(
        "guard_code: 26",
        "guard_code: 27",
        "guard_code: 34",
        "guard_code: 35",
        "guard_code: 47",
        "System.Threading.Mutex",
        "verify-unity-script-compilation.ps1",
        "console_matched_count"
    )) {
    if (-not $safeUnityText.Contains($sentinel)) {
        throw "safe-unity state sentinel is missing: $sentinel"
    }
}

$runnerText = [System.IO.File]::ReadAllText(
    (Join-Path $toolsRoot "invoke-unity-editor-runner.ps1")
)
foreach ($sentinel in @(
        'DependencyScriptPaths -split ";"',
        "Assert-AssembliesCurrentBeforeCleanup",
        "[switch]`$Concise",
        "[switch]`$BatchRefresh"
    )) {
    if (-not $runnerText.Contains($sentinel)) {
        throw "Editor runner contract sentinel is missing: $sentinel"
    }
}

$unityProcessOutput = @(
    & (Join-Path $toolsRoot "unity-process-report.ps1") -SelfTest
) -join "`n"
if (-not $unityProcessOutput.Contains("unity_process_report_self_test: passed")) {
    throw "unity-process-report redaction self-test failed."
}

$manifestOutput = @(
    & (Join-Path $toolsRoot "verify-unity-source-manifest.ps1") -SelfTest
) -join "`n"
if (-not $manifestOutput.Contains("unity_source_manifest_self_test: passed")) {
    throw "Unity source manifest deletion self-test failed."
}
if (-not $runnerText.Contains("verify-unity-source-manifest.ps1")) {
    throw "Editor runner must verify deleted C# sources before incremental import."
}

Write-Output "command_tool_test_module: unity passed"
