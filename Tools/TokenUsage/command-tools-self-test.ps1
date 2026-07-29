[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$testRoot = Join-Path $PSScriptRoot "CommandToolSelfTests"
$testFiles = @(
    Get-ChildItem -LiteralPath $testRoot -Filter "*.tests.ps1" -File |
        Sort-Object -Property Name
)

if ($testFiles.Count -eq 0) {
    throw "No command tool self-test modules were found: $testRoot"
}

foreach ($testFile in $testFiles) {
    & $testFile.FullName | Out-Null
}

Write-Output ("command_tools_self_test: passed; modules={0}" -f $testFiles.Count)
