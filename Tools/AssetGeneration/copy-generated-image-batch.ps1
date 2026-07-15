param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $true)][string]$DestinationDirectory,
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$allowedDestinationRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "Assets\AreaSurvivors\Sprites\External"))
$sourceRoot = [System.IO.Path]::GetFullPath($SourceDirectory)
$manifestFullPath = [System.IO.Path]::GetFullPath($ManifestPath)
$destinationRoot = [System.IO.Path]::GetFullPath($DestinationDirectory)

if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
    throw "SourceDirectory does not exist: $sourceRoot"
}
if (-not (Test-Path -LiteralPath $manifestFullPath -PathType Leaf)) {
    throw "ManifestPath does not exist: $manifestFullPath"
}
if (-not (Test-Path -LiteralPath $destinationRoot -PathType Container)) {
    throw "DestinationDirectory does not exist: $destinationRoot"
}
if (-not $destinationRoot.StartsWith($allowedDestinationRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "DestinationDirectory must stay under $allowedDestinationRoot"
}

$entries = @()
$lineNumber = 0
foreach ($line in [System.IO.File]::ReadAllLines($manifestFullPath, [System.Text.Encoding]::UTF8)) {
    $lineNumber++
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) { continue }
    $parts = $trimmed.Split([char]'|')
    if ($parts.Count -ne 2) {
        throw "Manifest line $lineNumber must be source-file|destination-file."
    }
    $sourceName = $parts[0].Trim()
    $destinationName = $parts[1].Trim()
    if ([System.IO.Path]::GetFileName($sourceName) -ne $sourceName -or [System.IO.Path]::GetFileName($destinationName) -ne $destinationName) {
        throw "Manifest line $lineNumber must contain file names only."
    }
    if ([System.IO.Path]::GetExtension($sourceName) -ne ".png" -or [System.IO.Path]::GetExtension($destinationName) -ne ".png") {
        throw "Manifest line $lineNumber must map PNG files."
    }
    $sourcePath = Join-Path $sourceRoot $sourceName
    $destinationPath = Join-Path $destinationRoot $destinationName
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Generated source is missing at manifest line ${lineNumber}: $sourcePath"
    }
    $entries += [PSCustomObject]@{ Source = $sourcePath; Destination = $destinationPath }
}

if ($entries.Count -eq 0) {
    throw "Manifest contains no image mappings: $manifestFullPath"
}

if (-not $ValidateOnly) {
    foreach ($entry in $entries) {
        Copy-Item -LiteralPath $entry.Source -Destination $entry.Destination -Force -ErrorAction Stop
    }
}

Write-Output ("validated={0}; copied={1}; source={2}; destination={3}" -f $entries.Count, ($(if ($ValidateOnly) { 0 } else { $entries.Count })), $sourceRoot, $destinationRoot)
