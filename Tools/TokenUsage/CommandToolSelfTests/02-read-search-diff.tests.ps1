$ErrorActionPreference = "Stop"

$toolsRoot = Split-Path $PSScriptRoot -Parent

function Assert-Parameter {
    param(
        [Parameter(Mandatory = $true)][string]$ScriptName,
        [Parameter(Mandatory = $true)][string[]]$Required,
        [string[]]$Forbidden = @()
    )

    $command = Get-Command (Join-Path $toolsRoot $ScriptName)
    foreach ($name in $Required) {
        if (-not $command.Parameters.ContainsKey($name)) {
            throw "$ScriptName is missing required parameter: $name"
        }
    }
    foreach ($name in $Forbidden) {
        if ($command.Parameters.ContainsKey($name)) {
            throw "$ScriptName exposes forbidden parameter: $name"
        }
    }
}

Assert-Parameter -ScriptName "safe-read.ps1" `
    -Required @("Path", "Pattern", "LiteralPattern", "StartLine", "EndLine", "Last") `
    -Forbidden @("File")
Assert-Parameter -ScriptName "safe-read-batch.ps1" `
    -Required @("Path", "Ranges") `
    -Forbidden @("File", "Requests")
Assert-Parameter -ScriptName "safe-search.ps1" `
    -Required @("Path", "Pattern", "FilesOnly", "HitSummary") `
    -Forbidden @("Root", "Context")
Assert-Parameter -ScriptName "focused-search.ps1" `
    -Required @("Path", "Pattern", "Context", "MaxMatchesPerFile", "Extension") `
    -Forbidden @("Query", "FilesOnly")
Assert-Parameter -ScriptName "safe-diff.ps1" `
    -Required @("Path", "MaxLines", "PrintOutput")
Assert-Parameter -ScriptName "scoped-diff-check.ps1" `
    -Required @("Path", "Cached", "ExcludeUnityMeta") `
    -Forbidden @("Mode", "SummaryOnly")

$safeReadText = [System.IO.File]::ReadAllText((Join-Path $toolsRoot "safe-read.ps1"))
foreach ($sentinel in @(
        "[switch]`$LiteralPattern",
        "guard_code: 36",
        "guard_code: 37",
        "guard_code: 39"
    )) {
    if (-not $safeReadText.Contains($sentinel)) {
        throw "safe-read guard sentinel is missing: $sentinel"
    }
}

$safeSearchText = [System.IO.File]::ReadAllText((Join-Path $toolsRoot "safe-search.ps1"))
foreach ($sentinel in @(
        "does not accept -Root",
        "guard_code: 43",
        "guard_code: 44",
        ".sandbox-secrets"
    )) {
    if (-not $safeSearchText.Contains($sentinel)) {
        throw "safe-search guard sentinel is missing: $sentinel"
    }
}

$focusedText = [System.IO.File]::ReadAllText((Join-Path $toolsRoot "focused-search.ps1"))
if (-not $focusedText.Contains("Each -Path item must exist") -or
    -not $focusedText.Contains('$maximumTopFiles = 2') -or
    -not $focusedText.Contains('$maximumContext = 4') -or
    -not $focusedText.Contains('$maximumMatchesPerFile = 2') -or
    -not $focusedText.Contains("exit 0")) {
    throw "focused-search path or no-match contract is missing."
}

if (-not $safeReadText.Contains('$isUnityReport') -or
    -not $safeReadText.Contains('{ 24 }') -or
    -not $safeReadText.Contains("guard_code: 47")) {
    throw "safe-read Unity Report output cap is missing."
}

$safeDiffText = [System.IO.File]::ReadAllText((Join-Path $toolsRoot "safe-diff.ps1"))
$safeCommandText = [System.IO.File]::ReadAllText(
    (Join-Path $toolsRoot "Invoke-AreaSafeCommand.ps1")
)
if (-not $safeDiffText.Contains("[Math]::Min(`$MaxLines, 40)") -or
    -not $safeCommandText.Contains("[diff output truncated:")) {
    throw "Unity text diff output cap is missing."
}

$diffText = [System.IO.File]::ReadAllText((Join-Path $toolsRoot "scoped-diff-check.ps1"))
if (-not $diffText.Contains('$rawItem -split ";"') -or
    -not $diffText.Contains('"--cached --check"')) {
    throw "scoped-diff semicolon or cached contract is missing."
}

Write-Output "command_tool_test_module: read-search-diff passed"
