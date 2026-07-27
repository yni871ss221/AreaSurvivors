param(
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$reviewRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "Docs\ArtReview\OpeningStoryHighQuality"))
$sourceRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "Assets\AreaSurvivors\Sprites\External\UI\OpeningStory"))
$gameRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "Assets\AreaSurvivors\Sprites\Generated\UI\OpeningStory"))

$requiredDirectories = @($reviewRoot, $sourceRoot, $gameRoot)
foreach ($directory in $requiredDirectories) {
    if (-not $directory.StartsWith($projectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Opening story image directory must stay inside the project: $directory"
    }
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw "Opening story image directory does not exist: $directory"
    }
}

$items = 1..6 | ForEach-Object {
    $number = "{0:D2}" -f $_
    [PSCustomObject]@{
        Review = Join-Path $reviewRoot ("OpeningStory{0}_HQ.png" -f $number)
        Source = Join-Path $sourceRoot ("OpeningStory{0}Source.png" -f $number)
        Game = Join-Path $gameRoot ("OpeningStory{0}.png" -f $number)
    }
}

foreach ($item in $items) {
    if (-not (Test-Path -LiteralPath $item.Review -PathType Leaf)) {
        throw "Approved review image is missing: $($item.Review)"
    }
    if (-not (Test-Path -LiteralPath $item.Source -PathType Leaf)) {
        throw "Existing source target is missing: $($item.Source)"
    }
    if (-not (Test-Path -LiteralPath $item.Game -PathType Leaf)) {
        throw "Existing game target is missing: $($item.Game)"
    }
}

if (-not $ValidateOnly) {
    foreach ($item in $items) {
        Copy-Item -LiteralPath $item.Review -Destination $item.Source -Force -ErrorAction Stop
        Copy-Item -LiteralPath $item.Review -Destination $item.Game -Force -ErrorAction Stop
    }

    foreach ($item in $items) {
        $reviewHash = (Get-FileHash -LiteralPath $item.Review -Algorithm SHA256).Hash
        $sourceHash = (Get-FileHash -LiteralPath $item.Source -Algorithm SHA256).Hash
        $gameHash = (Get-FileHash -LiteralPath $item.Game -Algorithm SHA256).Hash
        if ($reviewHash -ne $sourceHash -or $reviewHash -ne $gameHash) {
            throw "Opening story image hash mismatch after copy: $($item.Review)"
        }
    }
}

Write-Output ("opening_story_images_validated={0}; copied={1}" -f $items.Count, ($(if ($ValidateOnly) { 0 } else { $items.Count * 2 })))
