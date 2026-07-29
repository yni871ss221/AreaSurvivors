$ErrorActionPreference = "Stop"

$toolsRoot = Split-Path $PSScriptRoot -Parent
$projectRoot = Split-Path (Split-Path $toolsRoot -Parent) -Parent

$parseFailures = New-Object System.Collections.Generic.List[string]
$powerShellFiles = @(
    Get-ChildItem -LiteralPath $toolsRoot -Filter "*.ps1" -File -Recurse
)
foreach ($file in $powerShellFiles) {
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile(
        $file.FullName,
        [ref]$tokens,
        [ref]$errors
    ) | Out-Null
    if ($errors.Count -gt 0) {
        $parseFailures.Add(("{0}: {1}" -f $file.FullName, $errors[0].Message))
    }
}
if ($parseFailures.Count -gt 0) {
    throw ("PowerShell parse failures:`n" + ($parseFailures -join "`n"))
}

$requiredDocs = @{
    "Docs/AgentRules/token-tools.md" = @(
        "Docs/AgentRules/code-navigation.md",
        "Docs/AgentRules/command-wrappers.md",
        "Docs/AgentRules/unity-command-tools.md",
        "Docs/AgentRules/token-measurement.md"
    )
    "AGENTS.md" = @(
        "Docs/AgentRules/token-tools.md",
        "PowerShell"
    )
}
foreach ($entry in $requiredDocs.GetEnumerator()) {
    $path = Join-Path $projectRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required routing document is missing: $($entry.Key)"
    }
    $text = [System.IO.File]::ReadAllText($path)
    foreach ($sentinel in $entry.Value) {
        if (-not $text.Contains($sentinel)) {
            throw "Routing sentinel is missing from $($entry.Key): $sentinel"
        }
    }
}

$indexScript = Join-Path $toolsRoot "structure-index.ps1"
$indexText = [System.IO.File]::ReadAllText($indexScript)
foreach ($sentinel in @(
        'Assets\AreaSurvivors',
        '"Tools"',
        'StructureIndexTool\StructureIndexTool.csproj'
    )) {
    if (-not $indexText.Contains($sentinel)) {
        throw "Structure index root or implementation sentinel is missing: $sentinel"
    }
}
& $indexScript -Action SelfTest | Out-Null

$directEvalPattern = "unicli exec " + "Eval"
$unsafeDirectEvalScripts = @(
    Get-ChildItem -LiteralPath $toolsRoot -Filter "*.ps1" -File -Recurse |
        Where-Object {
            $_.Name -notlike "*.tests.ps1" -and
            $_.Name -ne "Invoke-AreaSafeUnity.ps1" -and
            [System.IO.File]::ReadAllText($_.FullName).Contains($directEvalPattern)
        }
)
if ($unsafeDirectEvalScripts.Count -gt 0) {
    throw ("Direct Eval entry found outside safe-unity: " +
        (($unsafeDirectEvalScripts | ForEach-Object FullName) -join ", "))
}

Write-Output "command_tool_test_module: foundation passed"
