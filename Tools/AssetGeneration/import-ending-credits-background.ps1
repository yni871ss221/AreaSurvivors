param(
    [Parameter(Mandatory = $true)][string]$GeneratedImagePath,
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$inputPath = [System.IO.Path]::GetFullPath($GeneratedImagePath)
$externalRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "Assets\AreaSurvivors\Sprites\External\UI\EndingCredits"))
$generatedRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "Assets\AreaSurvivors\Sprites\Generated\UI\EndingCredits"))
$sourcePath = Join-Path $externalRoot "EndingCreditsBackgroundSource.png"
$gamePath = Join-Path $generatedRoot "EndingCreditsBackground.png"

if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) {
    throw "Generated ending credits image does not exist: $inputPath"
}
if ([System.IO.Path]::GetExtension($inputPath) -ine ".png") {
    throw "Generated ending credits image must be a PNG: $inputPath"
}
foreach ($targetRoot in @($externalRoot, $generatedRoot)) {
    if (-not $targetRoot.StartsWith($projectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Ending credits target must stay inside the project: $targetRoot"
    }
}

Add-Type -AssemblyName System.Drawing
$image = [System.Drawing.Image]::FromFile($inputPath)
try {
    if ($image.Width -lt 1280 -or $image.Height -lt 720) {
        throw "Ending credits background is below the minimum 1280x720 resolution: $($image.Width)x$($image.Height)"
    }
    $aspect = [double]$image.Width / [double]$image.Height
    $targetAspect = 16.0 / 9.0
    if ([Math]::Abs($aspect - $targetAspect) -gt 0.01) {
        throw "Ending credits background must be approximately 16:9: $($image.Width)x$($image.Height)"
    }
    $dimensions = "$($image.Width)x$($image.Height)"
}
finally {
    $image.Dispose()
}

if (-not $ValidateOnly) {
    [System.IO.Directory]::CreateDirectory($externalRoot) | Out-Null
    [System.IO.Directory]::CreateDirectory($generatedRoot) | Out-Null
    Copy-Item -LiteralPath $inputPath -Destination $sourcePath -Force -ErrorAction Stop
    Copy-Item -LiteralPath $inputPath -Destination $gamePath -Force -ErrorAction Stop

    $inputHash = (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash
    $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    $gameHash = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash
    if ($inputHash -ne $sourceHash -or $inputHash -ne $gameHash) {
        throw "Ending credits background hash mismatch after import."
    }
}

Write-Output ("ending_credits_background_validated=true; dimensions={0}; copied={1}" -f $dimensions, ($(if ($ValidateOnly) { 0 } else { 2 })))
