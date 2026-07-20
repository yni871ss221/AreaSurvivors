param(
    [string]$SkillPath = 'C:\Users\yni87\.codex\skills\area-survivors-trailer-production'
)

$ErrorActionPreference = 'Stop'

$python = 'C:\Users\yni87\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$validator = 'C:\Users\yni87\.codex\skills\.system\skill-creator\scripts\quick_validate.py'
$dependencyPath = 'C:\Users\yni87\.cache\codex-skill-tools\pyyaml'
$skillFile = Join-Path $SkillPath 'SKILL.md'
$metadataFile = Join-Path $SkillPath 'agents\openai.yaml'

foreach ($required in @($python, $validator, $skillFile, $metadataFile, (Join-Path $dependencyPath 'yaml\__init__.py'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required validation file was not found: $required"
    }
}

$env:PYTHONPATH = $dependencyPath
& $python -X utf8 $validator $SkillPath
if ($LASTEXITCODE -ne 0) {
    throw "quick_validate.py failed with exit code $LASTEXITCODE"
}

$metadata = Get-Content -LiteralPath $metadataFile -Raw -Encoding UTF8
foreach ($requiredText in @(
    'display_name: "Area Survivors Trailer Production"',
    'short_description: "Create, revise, encode, and verify Steam trailer videos"',
    'default_prompt:',
    '$area-survivors-trailer-production'
)) {
    if (-not $metadata.Contains($requiredText)) {
        throw "agents/openai.yaml is missing required text: $requiredText"
    }
}

Write-Output 'agents/openai.yaml is valid for this skill.'
