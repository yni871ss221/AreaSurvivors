param(
    [string]$SkillPath = 'C:\Users\yni87\.codex\skills\area-survivors-trailer-production'
)

$ErrorActionPreference = 'Stop'

$python = 'C:\Users\yni87\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$generator = 'C:\Users\yni87\.codex\skills\.system\skill-creator\scripts\generate_openai_yaml.py'
$skillName = 'area-survivors-trailer-production'

foreach ($required in @($python, $generator, (Join-Path $SkillPath 'SKILL.md'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required file was not found: $required"
    }
}

& $python -X utf8 $generator $SkillPath `
    --name $skillName `
    --interface 'display_name=Area Survivors Trailer Production' `
    --interface 'short_description=Create, revise, encode, and verify Steam trailer videos' `
    --interface 'default_prompt=Use $area-survivors-trailer-production to create or revise an Area Survivors Steam trailer.'

if ($LASTEXITCODE -ne 0) {
    throw "generate_openai_yaml.py failed with exit code $LASTEXITCODE"
}
