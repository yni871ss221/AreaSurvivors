<#
.SYNOPSIS
Runs Codex skill-creator quick_validate with a UTF-8 Python environment.

.DESCRIPTION
Formal usage:
  invoke-skill-validator.ps1 -SelfTest
  invoke-skill-validator.ps1 -SkillPath <absolute-or-relative-skill-directory>

Windows Python may default to cp932, while skill files are UTF-8. This wrapper
fixes PYTHONUTF8=1 and verifies that the selected Python also provides PyYAML.
#>
param(
    [string]$SkillPath,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

if (-not $SelfTest -and [string]::IsNullOrWhiteSpace($SkillPath)) {
    throw "Specify -SkillPath or -SelfTest."
}

$python = Get-Command python -ErrorAction Stop
$validatorPath = Join-Path $env:USERPROFILE ".codex\skills\.system\skill-creator\scripts\quick_validate.py"
if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
    throw "Skill validator was not found: $validatorPath"
}

$previousPythonUtf8 = $env:PYTHONUTF8
try {
    $env:PYTHONUTF8 = "1"
    & $python.Source -c "import sys, yaml; assert sys.flags.utf8_mode == 1"
    if ($LASTEXITCODE -ne 0) {
        throw "Python UTF-8/PyYAML preflight failed with exit code $LASTEXITCODE."
    }

    if ($SelfTest) {
        Write-Output "skill_validator_self_test: passed"
        exit 0
    }

    $resolvedSkillPath = (Resolve-Path -LiteralPath $SkillPath -ErrorAction Stop).Path
    & $python.Source $validatorPath $resolvedSkillPath
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
} finally {
    $env:PYTHONUTF8 = $previousPythonUtf8
}

exit 0

