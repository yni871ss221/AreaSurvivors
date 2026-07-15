param(
    [ValidateRange(1, 300)][int]$TimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$markerPath = Join-Path $projectRoot "Library\AreaSafeUnity\player-directional-animator-validator.ok"

if (Test-Path -LiteralPath $markerPath -PathType Leaf) {
    Remove-Item -LiteralPath $markerPath -Force
}

& "$PSScriptRoot\Invoke-AreaSafeUnity.ps1" `
    -Action Menu `
    -MenuPath "Area Survivors/Validate/Player Directional Animator Migration" `
    -TimeoutSeconds $TimeoutSeconds
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
    throw "guard_code: 43; Player directional Animator validator did not create its success marker. Menu exit code alone is not a validation result. marker=$markerPath"
}

Write-Output "player_directional_animator_migration_validation: passed"
Write-Output "marker_path: $markerPath"
